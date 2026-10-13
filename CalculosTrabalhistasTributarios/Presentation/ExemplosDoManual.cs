using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.IO;

namespace CalculosTrabalhistasTributarios.Presentation;

/// <summary>
/// O exemplo de cada calculadora, com os dados fictícios das imagens do manual, no mesmo formato do histórico. Os arquivos
/// ficam em Presentation/Exemplos, um por janela, com o nome da chave do histórico, como "Calculadora.Rescisao.json".
/// </summary>
public static class ExemplosDoManual
{
    private const string Prefixo = "Exemplos.";
    private static readonly System.Reflection.Assembly Assembly = typeof(ExemplosDoManual).Assembly;

    public static IReadOnlyList<string> Chaves { get; } = Assembly.GetManifestResourceNames()
        .Where(nome => nome.StartsWith(Prefixo, StringComparison.Ordinal) && nome.EndsWith(".json", StringComparison.Ordinal))
        .Select(nome => nome[Prefixo.Length..^".json".Length]).ToArray();

    public static bool Existe(string chave) => Chaves.Contains(chave);

    public static DadosFormulario? Obter(string chave)
    {
        using var recurso = Assembly.GetManifestResourceStream($"{Prefixo}{chave}.json");
        if (recurso is null)
            return null;
        using var leitor = new StreamReader(recurso);
        return DadosFormulario.DeJson(leitor.ReadToEnd());
    }
}
