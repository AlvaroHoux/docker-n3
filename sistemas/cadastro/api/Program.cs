// =====================================================================
//  SISTEMA 1 - CADASTRO DE FUNCIONÁRIOS (API)
//  Guarda os funcionários no arquivo dados/funcionarios.json
// =====================================================================
using CadastroApi;

var builder = WebApplication.CreateBuilder(args);

// Porta: fora do Docker usa http://localhost:5001.
// Dentro do Docker, a imagem oficial do ASP.NET já define ASPNETCORE_HTTP_PORTS=8080
// (ou você pode definir ASPNETCORE_URLS) e este "if" é ignorado.
if (string.IsNullOrEmpty(builder.Configuration["urls"]) &&
    Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") is null)
{
    builder.WebHost.UseUrls("http://localhost:5001");
}

// CORS liberado: o front-end (outra porta) chama esta API direto do navegador.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton(new JsonStore<Funcionario>("funcionarios.json"));

var app = builder.Build();
app.UseCors();

// Qualquer erro vira uma resposta JSON legível (ex.: arquivo corrompido).
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        app.Logger.LogError("Erro em {Path}: {Msg}", ctx.Request.Path, ex.Message);
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { erro = ex.Message });
    }
});

var inicio = DateTime.Now;

app.MapGet("/", () => Results.Redirect("/api/info"));

app.MapGet("/health", (JsonStore<Funcionario> db) =>
{
    db.Ler(); // se o JSON estiver corrompido, o health também acusa erro
    return Results.Ok(new { status = "ok", sistema = "cadastro-api" });
});

app.MapGet("/api/info", (JsonStore<Funcionario> db) => new
{
    sistema = "Sistema 1 - Cadastro de Funcionários",
    maquina = Environment.MachineName, // dentro do Docker = ID curto do contêiner
    arquivoDeDados = db.Caminho,
    noArDesde = inicio
});

app.MapGet("/api/funcionarios", (JsonStore<Funcionario> db) => db.Ler().OrderBy(f => f.Id));

app.MapGet("/api/funcionarios/{id:int}", (int id, JsonStore<Funcionario> db) =>
    db.Ler().FirstOrDefault(f => f.Id == id) is { } f
        ? Results.Ok(f)
        : Results.NotFound(new { erro = $"Funcionário {id} não encontrado" }));

app.MapPost("/api/funcionarios", (FuncionarioDto dto, JsonStore<Funcionario> db) =>
{
    if (Validar(dto) is { } erro) return Results.BadRequest(new { erro });
    var lista = db.Ler();
    var novo = new Funcionario
    {
        Id = lista.Count == 0 ? 1 : lista.Max(f => f.Id) + 1,
        Nome = dto.Nome.Trim(),
        Cargo = dto.Cargo.Trim(),
        ValorHora = dto.ValorHora
    };
    lista.Add(novo);
    db.Salvar(lista);
    app.Logger.LogInformation("Funcionário cadastrado: {Id} - {Nome}", novo.Id, novo.Nome);
    return Results.Created($"/api/funcionarios/{novo.Id}", novo);
});

app.MapPut("/api/funcionarios/{id:int}", (int id, FuncionarioDto dto, JsonStore<Funcionario> db) =>
{
    if (Validar(dto) is { } erro) return Results.BadRequest(new { erro });
    var lista = db.Ler();
    var f = lista.FirstOrDefault(x => x.Id == id);
    if (f is null) return Results.NotFound(new { erro = $"Funcionário {id} não encontrado" });
    f.Nome = dto.Nome.Trim(); f.Cargo = dto.Cargo.Trim(); f.ValorHora = dto.ValorHora;
    db.Salvar(lista);
    return Results.Ok(f);
});

app.MapDelete("/api/funcionarios/{id:int}", (int id, JsonStore<Funcionario> db) =>
{
    var lista = db.Ler();
    if (lista.RemoveAll(f => f.Id == id) == 0) return Results.NotFound(new { erro = $"Funcionário {id} não encontrado" });
    db.Salvar(lista);
    return Results.NoContent();
});

// Cria 5 funcionários de exemplo (só se a lista estiver vazia).
app.MapPost("/api/seed", (JsonStore<Funcionario> db) =>
{
    var lista = db.Ler();
    if (lista.Count > 0) return Results.Ok(new { mensagem = "Já existem funcionários; nada foi criado.", total = lista.Count });
    lista.AddRange(new[]
    {
        new Funcionario { Id = 1, Nome = "Ana Souza",      Cargo = "Desenvolvedora",  ValorHora = 45.00m },
        new Funcionario { Id = 2, Nome = "Bruno Lima",     Cargo = "Analista de QA",  ValorHora = 38.50m },
        new Funcionario { Id = 3, Nome = "Carla Mendes",   Cargo = "Gerente de TI",   ValorHora = 70.00m },
        new Funcionario { Id = 4, Nome = "Diego Ferreira", Cargo = "Suporte Técnico", ValorHora = 25.00m },
        new Funcionario { Id = 5, Nome = "Elisa Rocha",    Cargo = "DevOps",          ValorHora = 55.00m },
    });
    db.Salvar(lista);
    return Results.Ok(new { mensagem = "5 funcionários criados.", total = lista.Count });
});

// TESTE DE RESILIÊNCIA: derruba o processo de propósito (simula um crash).
// Com "restart: unless-stopped" no compose, o Docker sobe o contêiner de novo sozinho.
app.MapPost("/api/admin/derrubar", () =>
{
    app.Logger.LogCritical("Derrubando a API de propósito (teste de resiliência)...");
    _ = Task.Run(async () => { await Task.Delay(300); Environment.Exit(1); });
    return Results.Ok(new { mensagem = "A API vai cair em instantes (exit code 1)." });
});

app.Run();

static string? Validar(FuncionarioDto dto)
{
    if (string.IsNullOrWhiteSpace(dto.Nome)) return "Nome é obrigatório";
    if (string.IsNullOrWhiteSpace(dto.Cargo)) return "Cargo é obrigatório";
    if (dto.ValorHora <= 0) return "Valor da hora deve ser maior que zero";
    return null;
}

namespace CadastroApi
{
    public class Funcionario
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string Cargo { get; set; } = "";
        public decimal ValorHora { get; set; }
    }

    public record FuncionarioDto(string Nome, string Cargo, decimal ValorHora);
}
