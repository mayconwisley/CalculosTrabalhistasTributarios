using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Pensao;

/// <summary>Apura as pensões na ordem informada, preservando a base própria de cada regra.</summary>
public static class MotorPensoes
{
    public static IReadOnlyList<ParcelaBeneficiarioPensao> Calcular(IReadOnlyList<RegraPensao> regras,
        bool sucessiva, decimal rendimentos, decimal inss, decimal imposto, decimal salarioMinimo)
    {
        var parcelas = new List<ParcelaBeneficiarioPensao>(regras.Count);
        var anteriores = 0m;
        foreach (var regra in regras)
        {
            var desconto = sucessiva ? anteriores : 0m;
            var basePensao = regra.Base switch
            {
                BasePensao.RendimentosLiquidos => Math.Max(0m, rendimentos - inss - imposto - desconto),
                BasePensao.RendimentosBrutos => Math.Max(0m, rendimentos - desconto),
                BasePensao.SalarioMinimo => salarioMinimo,
                _ => regra.Valor
            };
            var valor = regra.EhPercentual ? CalculadoraTributacao.Arredondar(basePensao * regra.Percentual / 100m) : regra.Valor;
            parcelas.Add(new(regra, basePensao, valor));
            anteriores += valor;
        }
        return parcelas;
    }
}
