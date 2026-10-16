namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="ValorBruto">Rendimento do mês, base do IRRF; na aposentadoria, inclui a parcela isenta.</param>
/// <param name="BaseInss">Remuneração sobre a qual incidem o INSS, limitado ao teto, e o FGTS, sem teto. Zero na aposentadoria.</param>
/// <param name="Vinculo">Define a alíquota do FGTS: 8%, 2% no aprendiz e 8% mais 3,2% no doméstico; a aposentadoria não tem INSS nem FGTS.</param>
/// <param name="SessentaECincoAnos">Na aposentadoria ou pensão, o beneficiário completou 65 anos até a competência, o que dá direito à parcela isenta.</param>
/// <param name="DecimoTerceiro">Na aposentadoria ou pensão, o valor bruto é o 13º (abono anual), de tributação exclusiva e com parcela isenta própria.</param>
public sealed record SimularImpostoRequest(
    DateOnly Competencia,
    decimal ValorBruto,
    decimal BaseInss,
    int QuantidadeDependentes,
    VinculoSimulacao Vinculo = VinculoSimulacao.Empregado,
    bool SessentaECincoAnos = false,
    bool DecimoTerceiro = false);
