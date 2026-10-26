namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Os campos a que uma mensagem de validação das regras de cálculo se refere, para destacá-los no formulário com o que
/// corrigir. As regras não conhecem a tela: a ligação é pelo rótulo, sem o complemento entre parênteses, como a unidade.
/// A busca pelo rótulo fica em <c>CamposCitadosPorRotulo.cs</c>, compartilhado com o aplicativo Android.
/// </summary>
public static partial class CamposCitados
{
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
}
