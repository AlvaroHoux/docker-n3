// =====================================================================
//  SISTEMA 2 - CONTROLE DE PONTO (API)
//  Guarda as marcações no arquivo dados/ponto.json
//  Busca os funcionários no Sistema 1 (Cadastro) via HTTP.
// =====================================================================
using System.Globalization;
using PontoApi;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrEmpty(builder.Configuration["urls"]) &&
    Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") is null)
{
    builder.WebHost.UseUrls("http://localhost:5002");
}

// Endereço do Sistema 1. Fora do Docker: localhost. Dentro do Docker: o NOME DO SERVIÇO no compose.
var cadastroUrl = Environment.GetEnvironmentVariable("CADASTRO_API_URL") ?? "http://localhost:5001";

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton(new JsonStore<RegistroPonto>("ponto.json"));
builder.Services.AddHttpClient("cadastro", c =>
{
    c.BaseAddress = new Uri(cadastroUrl);
    c.Timeout = TimeSpan.FromSeconds(5); // se o Cadastro travar (docker pause), desistimos em 5s
});

var app = builder.Build();
app.UseCors();

app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (DependenciaForaDoArException ex)
    {
        app.Logger.LogWarning("Dependência indisponível: {Msg}", ex.Message);
        ctx.Response.StatusCode = 503; // Service Unavailable
        await ctx.Response.WriteAsJsonAsync(new { erro = ex.Message });
    }
    catch (Exception ex)
    {
        app.Logger.LogError("Erro em {Path}: {Msg}", ctx.Request.Path, ex.Message);
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { erro = ex.Message });
    }
});

var inicio = DateTime.Now;

// ----- Funções que conversam com o Sistema 1 -----
async Task<List<FuncionarioRemoto>> BuscarFuncionarios(IHttpClientFactory f) =>
    await ChamarCadastro<List<FuncionarioRemoto>>(f, "/api/funcionarios") ?? new();

async Task<FuncionarioRemoto?> BuscarFuncionario(IHttpClientFactory f, int id) =>
    await ChamarCadastro<FuncionarioRemoto>(f, $"/api/funcionarios/{id}");

async Task<T?> ChamarCadastro<T>(IHttpClientFactory fabrica, string caminho)
{
    var http = fabrica.CreateClient("cadastro");
    try
    {
        var resp = await http.GetAsync(caminho);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return default;
        if (!resp.IsSuccessStatusCode)
            throw new DependenciaForaDoArException($"Sistema de Cadastro respondeu com erro {(int)resp.StatusCode} ({cadastroUrl}).");
        return await resp.Content.ReadFromJsonAsync<T>();
    }
    catch (TaskCanceledException)
    {
        throw new DependenciaForaDoArException($"Sistema de Cadastro não respondeu em 5 segundos ({cadastroUrl}). Ele está pausado ou travado?");
    }
    catch (HttpRequestException ex)
    {
        throw new DependenciaForaDoArException($"Sistema de Cadastro indisponível ({cadastroUrl}): {ex.Message}");
    }
}

// ----- Endpoints -----
app.MapGet("/", () => Results.Redirect("/api/info"));

app.MapGet("/health", async (JsonStore<RegistroPonto> db, IHttpClientFactory f) =>
{
    db.Ler();
    string cadastro;
    try { await BuscarFuncionarios(f); cadastro = "ok"; }
    catch (DependenciaForaDoArException ex) { cadastro = "FORA DO AR: " + ex.Message; }
    return Results.Ok(new { status = "ok", sistema = "ponto-api", dependencias = new { cadastro } });
});

app.MapGet("/api/info", (JsonStore<RegistroPonto> db) => new
{
    sistema = "Sistema 2 - Controle de Ponto",
    maquina = Environment.MachineName,
    arquivoDeDados = db.Caminho,
    cadastroApiUrl = cadastroUrl,
    noArDesde = inicio
});

// Repassa a lista de funcionários do Sistema 1 (o front do ponto usa isso).
app.MapGet("/api/funcionarios", async (IHttpClientFactory f) => await BuscarFuncionarios(f));

// Lista marcações (filtros opcionais). NÃO depende do Cadastro: lê só o ponto.json.
app.MapGet("/api/ponto", (int? funcionarioId, string? inicio, string? fim, JsonStore<RegistroPonto> db) =>
{
    var q = db.Ler().AsEnumerable();
    if (funcionarioId is not null) q = q.Where(r => r.FuncionarioId == funcionarioId);
    if (!string.IsNullOrEmpty(inicio)) q = q.Where(r => string.CompareOrdinal(r.Data, inicio) >= 0);
    if (!string.IsNullOrEmpty(fim)) q = q.Where(r => string.CompareOrdinal(r.Data, fim) <= 0);
    return q.OrderBy(r => r.Data).ThenBy(r => r.FuncionarioId);
});

// Lança uma marcação. Valida o funcionário no Sistema 1 (se ele estiver fora, retorna 503).
app.MapPost("/api/ponto", async (RegistroDto dto, JsonStore<RegistroPonto> db, IHttpClientFactory f) =>
{
    if (!DateOnly.TryParseExact(dto.Data, "yyyy-MM-dd", out _)) return Results.BadRequest(new { erro = "Data inválida (use AAAA-MM-DD)" });
    if (!TimeOnly.TryParseExact(dto.Entrada, "HH:mm", out var ent)) return Results.BadRequest(new { erro = "Entrada inválida (use HH:mm)" });
    if (!TimeOnly.TryParseExact(dto.Saida, "HH:mm", out var sai)) return Results.BadRequest(new { erro = "Saída inválida (use HH:mm)" });
    if (sai <= ent) return Results.BadRequest(new { erro = "A saída deve ser depois da entrada" });

    var func = await BuscarFuncionario(f, dto.FuncionarioId);
    if (func is null) return Results.BadRequest(new { erro = $"Funcionário {dto.FuncionarioId} não existe no Cadastro" });

    var lista = db.Ler();
    if (lista.Any(r => r.FuncionarioId == dto.FuncionarioId && r.Data == dto.Data))
        return Results.Conflict(new { erro = $"{func.Nome} já tem ponto lançado em {dto.Data}" });

    var novo = NovoRegistro(lista, func, dto.Data, ent, sai);
    lista.Add(novo);
    db.Salvar(lista);
    app.Logger.LogInformation("Ponto lançado: {Nome} em {Data} ({Horas}h)", func.Nome, dto.Data, novo.HorasTrabalhadas);
    return Results.Created($"/api/ponto/{novo.Id}", novo);
});

app.MapDelete("/api/ponto/{id:int}", (int id, JsonStore<RegistroPonto> db) =>
{
    var lista = db.Ler();
    if (lista.RemoveAll(r => r.Id == id) == 0) return Results.NotFound(new { erro = $"Registro {id} não encontrado" });
    db.Salvar(lista);
    return Results.NoContent();
});

// Gera automaticamente os últimos N dias úteis (padrão 30) para um funcionário ou para todos.
// Horários com pequena variação para ficar realista. Dias já lançados são mantidos.
app.MapPost("/api/ponto/gerar", async (GerarDto dto, JsonStore<RegistroPonto> db, IHttpClientFactory f) =>
{
    var dias = dto.Dias is > 0 and <= 120 ? dto.Dias.Value : 30;
    var funcionarios = dto.FuncionarioId is null
        ? await BuscarFuncionarios(f)
        : (await BuscarFuncionario(f, dto.FuncionarioId.Value)) is { } un ? new List<FuncionarioRemoto> { un } : new();
    if (funcionarios.Count == 0) return Results.BadRequest(new { erro = "Nenhum funcionário encontrado no Cadastro" });

    var datasUteis = new List<DateOnly>();
    var d = DateOnly.FromDateTime(DateTime.Today);
    while (datasUteis.Count < dias)
    {
        if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) datasUteis.Add(d);
        d = d.AddDays(-1);
    }

    var lista = db.Ler();
    var criados = 0;
    foreach (var func in funcionarios)
    {
        var rnd = new Random(func.Id); // mesma "sorte" para o mesmo funcionário
        foreach (var data in datasUteis.OrderBy(x => x))
        {
            var texto = data.ToString("yyyy-MM-dd");
            if (lista.Any(r => r.FuncionarioId == func.Id && r.Data == texto)) continue;
            var ent = new TimeOnly(8, 0).AddMinutes(rnd.Next(-15, 16));
            var sai = new TimeOnly(17, 0).AddMinutes(rnd.Next(-20, 61));
            lista.Add(NovoRegistro(lista, func, texto, ent, sai));
            criados++;
        }
    }
    db.Salvar(lista);
    return Results.Ok(new { mensagem = $"{criados} marcações criadas ({dias} dias úteis x {funcionarios.Count} funcionário(s)).", criados });
});

app.MapPost("/api/admin/derrubar", () =>
{
    app.Logger.LogCritical("Derrubando a API de propósito (teste de resiliência)...");
    _ = Task.Run(async () => { await Task.Delay(300); Environment.Exit(1); });
    return Results.Ok(new { mensagem = "A API vai cair em instantes (exit code 1)." });
});

app.Run();

static RegistroPonto NovoRegistro(List<RegistroPonto> lista, FuncionarioRemoto func, string data, TimeOnly ent, TimeOnly sai)
{
    // Regra simples: desconta 1h de almoço se trabalhou mais de 6h.
    var horas = (decimal)(sai - ent).TotalHours;
    if (horas > 6) horas -= 1;
    return new RegistroPonto
    {
        Id = lista.Count == 0 ? 1 : lista.Max(r => r.Id) + 1,
        FuncionarioId = func.Id,
        NomeFuncionario = func.Nome,
        Data = data,
        Entrada = ent.ToString("HH:mm", CultureInfo.InvariantCulture),
        Saida = sai.ToString("HH:mm", CultureInfo.InvariantCulture),
        HorasTrabalhadas = Math.Round(horas, 2)
    };
}

namespace PontoApi
{
    public class RegistroPonto
    {
        public int Id { get; set; }
        public int FuncionarioId { get; set; }
        public string NomeFuncionario { get; set; } = "";
        public string Data { get; set; } = "";     // AAAA-MM-DD
        public string Entrada { get; set; } = "";  // HH:mm
        public string Saida { get; set; } = "";    // HH:mm
        public decimal HorasTrabalhadas { get; set; }
    }

    public record RegistroDto(int FuncionarioId, string Data, string Entrada, string Saida);
    public record GerarDto(int? FuncionarioId, int? Dias);
    public record FuncionarioRemoto(int Id, string Nome, string Cargo, decimal ValorHora);

    public class DependenciaForaDoArException(string msg) : Exception(msg);
}
