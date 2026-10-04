using System.IO;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Presentation;

/// <summary>
/// Preferências do usuário, gravadas em um só arquivo. Todas passam por aqui: cada gravação reescreve o arquivo inteiro,
/// e uma classe que gravasse só as suas apagaria as das outras.
/// </summary>
public static class ConfiguracoesUsuario
{
    // O nome da pasta é o da primeira versão do aplicativo, mantido para preservar as preferências já salvas.
    private static readonly string Caminho = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CalculoIRRF", "settings.json");
    private static readonly Lazy<Configuracoes> Carregadas = new(Carregar);

    public static Configuracoes Atuais => Carregadas.Value;

    /// <summary>Aplica a alteração e grava; uma falha ao gravar não interrompe o uso, só deixa de lembrar a preferência.</summary>
    public static void Alterar(Action<Configuracoes> alterar)
    {
        alterar(Atuais);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Caminho)!);
            File.WriteAllText(Caminho, JsonSerializer.Serialize(Atuais));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static Configuracoes Carregar()
    {
        try
        {
            return File.Exists(Caminho) ? JsonSerializer.Deserialize<Configuracoes>(File.ReadAllText(Caminho)) ?? new Configuracoes() : new Configuracoes();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Configuracoes();
        }
    }
}
