using System.Globalization;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Presentation;

/// <summary>
/// Leitura de números digitados no padrão brasileiro. Sem vírgula, o ponto só é separador de milhar quando separa grupos
/// de três dígitos ("3.500", "1.234.567"); nos demais casos é a vírgula decimal digitada com ponto ("1.5", "62.5",
/// "2200.50"), em vez de ser ignorado e multiplicar o valor.
/// </summary>
public static partial class LeituraNumerica
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static bool TentarLer(string? texto, out decimal valor)
    {
        valor = 0m;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        var normalizado = texto.Trim();
        if (!normalizado.Contains(',') && normalizado.Contains('.') && !SoMilhar().IsMatch(normalizado))
        {
            if (normalizado.Count(caractere => caractere == '.') > 1)
                return false;
            normalizado = normalizado.Replace('.', ',');
        }
        return decimal.TryParse(normalizado, NumberStyles.Number, CulturaPtBr, out valor);
    }

    [GeneratedRegex(@"^[+-]?[1-9]\d{0,2}(\.\d{3})+$")]
    private static partial Regex SoMilhar();
}
