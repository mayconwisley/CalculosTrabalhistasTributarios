using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Competencia">Dezembro do ano do 13º: as tabelas do INSS e do recálculo do IRRF.</param>
/// <param name="Pagamento">Mês do pagamento do complemento, normalmente janeiro do ano seguinte.</param>
/// <param name="MediaPaga">Média de variáveis usada na 2ª parcela, em dezembro.</param>
public sealed record SimularComplementoDecimoTerceiroRequest(
    DateOnly Competencia,
    DateOnly Pagamento,
    decimal Salario,
    decimal MediaPaga,
    decimal VariaveisAteNovembro,
    decimal VariaveisDezembro,
    int Avos,
    int Dependentes,
    TributacaoComplementoDecimoTerceiro Tributacao);
