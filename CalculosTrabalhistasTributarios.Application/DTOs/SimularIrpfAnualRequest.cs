using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Declaração anual do imposto de renda a partir do ano-calendário 2026, com a tributação mínima das altas rendas.</summary>
/// <param name="RendimentosTributaveis">Salários, pró-labore, aluguéis e outros rendimentos tributáveis no ajuste anual; o 13º e a PLR ficam de fora.</param>
/// <param name="Instrucao">Despesas com instrução do titular e dos dependentes; o limite é por pessoa.</param>
/// <param name="PrevidenciaComplementar">PGBL e FAPI, dedutíveis até 12% dos rendimentos tributáveis.</param>
/// <param name="ImpostoPago">IRRF retido sobre os rendimentos tributáveis, carnê-leão e pagamentos complementares.</param>
/// <param name="Dividendos">Lucros e dividendos recebidos no ano, que entram na tributação mínima.</param>
/// <param name="IrrfDividendos">Retenção de 10% sobre os dividendos acima de R$ 50 mil no mês, compensada na tributação mínima.</param>
/// <param name="OutrosRendimentos">Rendimentos isentos ou de tributação exclusiva que entram na tributação mínima, como 13º, PLR, JCP e aplicações.</param>
/// <param name="ImpostoExclusivo">Imposto exclusivo ou definitivo pago sobre esses outros rendimentos.</param>
/// <param name="AliquotaEfetivaEmpresa">IRPJ e CSLL devidos pela empresa sobre o lucro, em %; zero para não calcular o redutor.</param>
public sealed record SimularIrpfAnualRequest(
    int Ano,
    decimal RendimentosTributaveis,
    decimal PrevidenciaOficial,
    int Dependentes,
    decimal DespesasMedicas,
    decimal Instrucao,
    decimal PrevidenciaComplementar,
    decimal PensaoAlimenticia,
    decimal ImpostoPago,
    decimal Dividendos,
    decimal IrrfDividendos,
    decimal OutrosRendimentos,
    decimal ImpostoExclusivo,
    decimal AliquotaEfetivaEmpresa,
    AliquotaNominalEmpresa AliquotaNominal);
