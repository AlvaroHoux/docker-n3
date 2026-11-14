// =====================================================================
//  SISTEMA 3 - FOLHA DE PONTO / CONTABILIZAÇÃO (API)
//  NÃO tem arquivo próprio: consulta o Sistema 1 (valor da hora)
//  e o Sistema 2 (dias trabalhados) e calcula a folha do funcionário.
// =====================================================================
using FolhaApi;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrEmpty(builder.Configuration["urls"]) &&
    Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") is null)
{
    builder.WebHost.UseUrls("http://localhost:5003");
}

var cadastroUrl = Environment.GetEnvironmentVariable("CADASTRO_API_URL") ?? "http://localhost:5001";
var pontoUrl = Environment.GetEnvironmentVariable("PONTO_API_URL") ?? "http://localhost:5002";

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpClient("cadastro", c => { c.BaseAddress = new Uri(cadastroUrl); c.Timeout = TimeSpan.FromSeconds(5); });
builder.Services.AddHttpClient("ponto", c => { c.BaseAddress = new Uri(pontoUrl); c.Timeout = TimeSpan.FromSeconds(5); });

var app = builder.Build();
app.UseCors();

app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (DependenciaForaDoArException ex)
    {
        app.Logger.LogWarning("Dependência indisponível: {Msg}", ex.Message);
        ctx.Response.StatusCode = 503;
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

async Task<T?> Chamar<T>(IHttpClientFactory fabrica, string cliente, string nomeSistema, string baseUrl, string caminho)
{
    var http = fabrica.CreateClient(cliente);
    try
    {
        var resp = await http.GetAsync(caminho);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return default;
        if (!resp.IsSuccessStatusCode)
            throw new DependenciaForaDoArException($"{nomeSistema} respondeu com erro {(int)resp.StatusCode} ({baseUrl}).");
        return await resp.Content.ReadFromJsonAsync<T>();
    }
    catch (TaskCanceledException)
    {
        throw new DependenciaForaDoArException($"{nomeSistema} não respondeu em 5 segundos ({baseUrl}). Ele está pausado ou travado?");
    }
    catch (HttpRequestException ex)
    {
        throw new DependenciaForaDoArException($"{nomeSistema} indisponível ({baseUrl}): {ex.Message}");
    }
}

Task<List<Funcionario>?> Funcionarios(IHttpClientFactory f) =>
    Chamar<List<Funcionario>>(f, "cadastro", "Sistema de Cadastro", cadastroUrl, "/api/funcionarios");

Task<Funcionario?> FuncionarioPorId(IHttpClientFactory f, int id) =>
    Chamar<Funcionario>(f, "cadastro", "Sistema de Cadastro", cadastroUrl, $"/api/funcionarios/{id}");

Task<List<RegistroPonto>?> Registros(IHttpClientFactory f, int? id, string? ini, string? fim)
{
    var filtros = new List<string>();
    if (id is not null) filtros.Add($"funcionarioId={id}");
    if (!string.IsNullOrEmpty(ini)) filtros.Add($"inicio={Uri.EscapeDataString(ini)}");
    if (!string.IsNullOrEmpty(fim)) filtros.Add($"fim={Uri.EscapeDataString(fim)}");
    return Chamar<List<RegistroPonto>>(f, "ponto", "Sistema de Ponto", pontoUrl, "/api/ponto?" + string.Join("&", filtros));
}

static FolhaDePonto Montar(Funcionario func, List<RegistroPonto> regs, string? ini, string? fim)
{
    var totalHoras = regs.Sum(r => r.HorasTrabalhadas);
    return new FolhaDePonto(
        func, ini, fim,
        regs.OrderBy(r => r.Data).ToList(),
        DiasTrabalhados: regs.Select(r => r.Data).Distinct().Count(),
        TotalHoras: Math.Round(totalHoras, 2),
        ValorBruto: Math.Round(totalHoras * func.ValorHora, 2),
        GeradaEm: DateTime.Now);
}

app.MapGet("/", () => Results.Redirect("/api/info"));

// Health mostra o estado das DUAS dependências — ótimo para o vídeo.
app.MapGet("/health", async (IHttpClientFactory f) =>
{
    string cadastro, ponto;
    try { await Funcionarios(f); cadastro = "ok"; } catch (DependenciaForaDoArException ex) { cadastro = "FORA DO AR: " + ex.Message; }
    try { await Registros(f, -1, null, null); ponto = "ok"; } catch (DependenciaForaDoArException ex) { ponto = "FORA DO AR: " + ex.Message; }
    return Results.Ok(new { status = "ok", sistema = "folha-api", dependencias = new { cadastro, ponto } });
});

app.MapGet("/api/info", () => new
{
    sistema = "Sistema 3 - Folha de Ponto",
    maquina = Environment.MachineName,
    cadastroApiUrl = cadastroUrl,
    pontoApiUrl = pontoUrl,
    noArDesde = inicio
});

app.MapGet("/api/funcionarios", async (IHttpClientFactory f) => await Funcionarios(f) ?? new());

// Folha de UM funcionário no período (inicio/fim no formato AAAA-MM-DD, opcionais).
app.MapGet("/api/folha/{funcionarioId:int}", async (int funcionarioId, string? inicio, string? fim, IHttpClientFactory f) =>
{
    var func = await FuncionarioPorId(f, funcionarioId);
    if (func is null) return Results.NotFound(new { erro = $"Funcionário {funcionarioId} não encontrado no Cadastro" });
    var regs = await Registros(f, funcionarioId, inicio, fim) ?? new();
    return Results.Ok(Montar(func, regs, inicio, fim));
});

// Resumo de TODOS os funcionários no período.
app.MapGet("/api/folha", async (string? inicio, string? fim, IHttpClientFactory f) =>
{
    var funcs = await Funcionarios(f) ?? new();
    var regs = await Registros(f, null, inicio, fim) ?? new();
    var folhas = funcs.Select(func => Montar(func, regs.Where(r => r.FuncionarioId == func.Id).ToList(), inicio, fim))
        .Select(x => new { x.Funcionario, x.DiasTrabalhados, x.TotalHoras, x.ValorBruto });
    return Results.Ok(new { inicio, fim, folhas, totalGeral = folhas.Sum(x => x.ValorBruto) });
});

app.MapPost("/api/admin/derrubar", () =>
{
    app.Logger.LogCritical("Derrubando a API de propósito (teste de resiliência)...");
    _ = Task.Run(async () => { await Task.Delay(300); Environment.Exit(1); });
    return Results.Ok(new { mensagem = "A API vai cair em instantes (exit code 1)." });
});

app.Run();

namespace FolhaApi
{
    public record Funcionario(int Id, string Nome, string Cargo, decimal ValorHora);
    public record RegistroPonto(int Id, int FuncionarioId, string NomeFuncionario, string Data, string Entrada, string Saida, decimal HorasTrabalhadas);
    public record FolhaDePonto(Funcionario Funcionario, string? Inicio, string? Fim, List<RegistroPonto> Registros,
        int DiasTrabalhados, decimal TotalHoras, decimal ValorBruto, DateTime GeradaEm);

    public class DependenciaForaDoArException(string msg) : Exception(msg);
}
