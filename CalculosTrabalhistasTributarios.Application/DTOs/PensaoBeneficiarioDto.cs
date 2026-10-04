
using CalculosTrabalhistasTributarios.Domain.Pensao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Base e valor da pensão de um beneficiário em uma iteração do cálculo.</summary>
public sealed record PensaoBeneficiarioDto(string Nome, RegraPensao Regra, decimal Base, decimal Pensao)
{
    /// <summary>A regra aplicada à base, como "30,00% do salário mínimo de R$ 1.621,00".</summary>
    public string Descrever(Func<decimal, string> moeda, Func<decimal, string> percentual) => Regra.Base switch
    {
        BasePensao.RendimentosLiquidos => $"{percentual(Regra.Percentual)} dos rendimentos líquidos",
        BasePensao.RendimentosBrutos => $"{percentual(Regra.Percentual)} dos rendimentos brutos de {moeda(Base)}",
        BasePensao.SalarioMinimo => $"{percentual(Regra.Percentual)} do salário mínimo de {moeda(Base)}",
        _ => "Valor fixo"
    };
}
