using System.Globalization;
using System.Text.RegularExpressions;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Os campos a que uma mensagem de validação das regras de cálculo se refere, para destacá-los no formulário com o que
/// corrigir. As regras não conhecem a tela: a ligação é pelo rótulo, sem o complemento entre parênteses, como a unidade.
/// </summary>
public static partial class CamposCitados
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Uma citação explícita, como "Valor solicitado: informe..." ou "O campo "Salário" ...", aponta só aquele campo.
    /// Sem ela, valem os rótulos que aparecem no texto, como "salário líquido desejado" em "Informe o salário líquido
    /// desejado"; um rótulo dentro de outro maior que também aparece, como "Salário" nesse exemplo, é desconsiderado.
    /// </summary>
    public static IReadOnlyList<(CampoViewModel Campo, string Mensagem)> Encontrar(string mensagem, IEnumerable<CampoViewModel> campos)
    {
        var visiveis = campos.Where(campo => campo.Visivel).ToList();
        var candidatos = visiveis.Where(campo => campo is CampoTextoViewModel or CampoOpcaoViewModel).Select(campo => (campo, campo.Rotulo)).ToList();
        var grades = visiveis.Where(campo => campo is not (CampoTextoViewModel or CampoOpcaoViewModel or CampoAtualizacaoCotacoesViewModel)).ToList();
        return Encontrar(mensagem, candidatos, grades.Count == 1 ? grades[0] : null);
    }

    /// <summary>
    /// A mesma busca para as janelas próprias, como a do débito judicial, que identificam os campos pelo nome da propriedade.
    /// A grade, quando há uma, recebe a mensagem que fala das suas linhas sem citar outro campo.
    /// </summary>
    public static IReadOnlyList<(T Campo, string Mensagem)> Encontrar<T>(string mensagem, IEnumerable<(T Campo, string Rotulo)> campos, T? grade) where T : class
    {
        var candidatos = campos.Select(campo => (campo.Campo, Rotulo: campo.Rotulo.Split(" (")[0].Trim()))
            .Where(candidato => candidato.Rotulo.Length > 0)
            .ToList();

        foreach (var (campo, rotulo) in candidatos)
        {
            if (mensagem.StartsWith(rotulo + ":", StringComparison.CurrentCultureIgnoreCase))
                return [(campo, Maiuscula(mensagem[(rotulo.Length + 1)..].Trim()))];
            if (mensagem.Contains($"\"{rotulo}", StringComparison.CurrentCultureIgnoreCase) || mensagem.Contains($"“{rotulo}", StringComparison.CurrentCultureIgnoreCase))
                return [(campo, mensagem)];
        }

        // Os campos com valor negativo já são apontados na leitura do formulário; o trecho da regra que lista quem não pode
        // ser negativo citaria campos que estão certos.
        var texto = string.Join(" ", Regex.Split(mensagem, @"(?<=[.;])\s+").Where(trecho => !trecho.Contains("negativ", StringComparison.OrdinalIgnoreCase)));

        // Depois de preposição, o rótulo só complementa outro termo, como "ano-base" em "remuneração média do ano-base".
        var ocorrencias = candidatos
            .Select(candidato => (candidato.Campo, Trechos: Regex.Matches(texto, $@"(?<![\p{{L}}\d]){Regex.Escape(candidato.Rotulo)}(?![\p{{L}}\d])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Where(ocorrencia => !Preposicao().IsMatch(texto[..ocorrencia.Index]))
                .Select(ocorrencia => (Inicio: ocorrencia.Index, Fim: ocorrencia.Index + ocorrencia.Length)).ToList()))
            .Where(candidato => candidato.Trechos.Count > 0)
            .ToList();
        var citados = ocorrencias
            .Where(candidato => candidato.Trechos.Any(trecho => !ocorrencias.Any(outro => outro.Campo != candidato.Campo
                && outro.Trechos.Any(maior => maior.Fim - maior.Inicio > trecho.Fim - trecho.Inicio && maior.Inicio <= trecho.Inicio && maior.Fim >= trecho.Fim))))
            .Select(candidato => (candidato.Campo, mensagem))
            .ToList();
        if (citados.Count > 0)
            return citados;

        // Uma grade, como a das competências ou dos períodos, não tem um rótulo a citar: sem outro campo citado, a mensagem
        // que fala das suas linhas é dela.
        return grade is not null && LinhasDeGrade().IsMatch(texto) ? [(grade, mensagem)] : [];
    }

    private static string Maiuscula(string texto) => texto.Length == 0 ? texto : char.ToUpper(texto[0], Cultura) + texto[1..];

    [GeneratedRegex(@"\b(compet[eê]ncias?|per[ií]odos?|meses|lan[cç]amentos?|parcelas?|v[ií]nculos?|aportes?|linhas?|dep[oó]sitos?|Gere)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LinhasDeGrade();

    [GeneratedRegex(@"(?<![\p{L}])(de|do|da|dos|das|no|na|nos|nas|em|ao|aos|à|às|pelo|pela|pelos|pelas|por|sobre)\s+$", RegexOptions.IgnoreCase)]
    private static partial Regex Preposicao();
}
