using System.Text.Json;

namespace PontoApi;

/// <summary>
/// "Banco de dados" do sistema: uma lista de objetos salva em um arquivo .json.
/// O caminho da pasta vem da variável de ambiente DATA_DIR (padrão: ./dados ao lado do executável).
/// </summary>
public class JsonStore<T>
{
    private readonly string _arquivo;
    private readonly object _trava = new();
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonStore(string nomeArquivo)
    {
        var pasta = Environment.GetEnvironmentVariable("DATA_DIR") ?? Path.Combine(AppContext.BaseDirectory, "dados");
        Directory.CreateDirectory(pasta);
        _arquivo = Path.GetFullPath(Path.Combine(pasta, nomeArquivo));
        if (!File.Exists(_arquivo)) File.WriteAllText(_arquivo, "[]");
    }

    public string Caminho => _arquivo;

    public List<T> Ler()
    {
        lock (_trava)
        {
            // Se alguém apagou o arquivo, recria vazio (os dados antigos se perdem!)
            if (!File.Exists(_arquivo)) File.WriteAllText(_arquivo, "[]");
            var texto = File.ReadAllText(_arquivo);
            try
            {
                return JsonSerializer.Deserialize<List<T>>(texto, Opcoes) ?? new List<T>();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"O arquivo {_arquivo} está corrompido (JSON inválido): {ex.Message}");
            }
        }
    }

    public void Salvar(List<T> itens)
    {
        lock (_trava)
        {
            File.WriteAllText(_arquivo, JsonSerializer.Serialize(itens, Opcoes));
        }
    }
}
