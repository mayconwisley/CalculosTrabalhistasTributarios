using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Avos">Meses do ano com 15 dias ou mais de trabalho, de 1 a 12.</param>
/// <param name="ValorAdiantamento">Usado apenas quando <paramref name="Adiantamento"/> é <see cref="AdiantamentoDecimoTerceiro.ValorInformado"/>.</param>
/// <param name="Pensao">Pensão alimentícia sobre o 13º integral, descontada na 2ª parcela; nula quando não há.</param>
/// <param name="PrevidenciaComplementar">Contribuição do trabalhador à previdência complementar sobre o 13º, deduzida do IRRF até 12% do 13º.</param>
public sealed record SimularDecimoTerceiroRequest(
    DateOnly Competencia,
    decimal Salario,
    decimal Medias,
    int Avos,
    int Dependentes,
    AdiantamentoDecimoTerceiro Adiantamento,
    decimal ValorAdiantamento,
    RegraPensao? Pensao = null,
    decimal PrevidenciaComplementar = 0m);
