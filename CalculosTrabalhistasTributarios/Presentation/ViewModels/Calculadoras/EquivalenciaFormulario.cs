using System.Globalization;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Compara valores do formulário pelo que significam, não pelo texto exibido. Os campos numéricos limpam o zero ao receber
/// o foco e o formatam ao sair ("3500" vira "3.500,00"); só passar pelo campo não é uma alteração dos dados, nem deve
/// apagar o erro apontado nele ou dar o resultado como desatualizado.
/// </summary>
internal static partial class EquivalenciaFormulario
{
    public static bool Equivalentes(string primeiro, string segundo) => Normalizar(primeiro) == Normalizar(segundo);

    public static bool Equivalentes(IReadOnlyDictionary<string, string> primeiro, IReadOnlyDictionary<string, string> segundo) =>
        primeiro.Count == segundo.Count && primeiro.All(item => segundo.TryGetValue(item.Key, out var valor) && Equivalentes(item.Value, valor));

    private static string Normalizar(string valor)
    {
        var texto = valor.Trim();
        if (texto.Length == 0 || HorasZeradas().IsMatch(texto))
            return string.Empty;
        if (LeituraNumerica.TentarLer(texto, out var numero))
            return numero == 0m ? string.Empty : numero.ToString("G29", CultureInfo.InvariantCulture);
        // As grades guardam as linhas em JSON; nelas, o zero limpo pelo foco aparece como texto vazio.
        return ZeroEmJson().Replace(texto, "\"\"");
    }

    [GeneratedRegex(@"^0+:00$")]
    private static partial Regex HorasZeradas();

    [GeneratedRegex(@"""(?:0+(?:,0+)?|0+:00)""")]
    private static partial Regex ZeroEmJson();
}
