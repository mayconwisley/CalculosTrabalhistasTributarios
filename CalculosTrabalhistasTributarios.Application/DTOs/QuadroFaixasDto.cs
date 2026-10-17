namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>
/// Faixas de um cálculo progressivo em forma de tabela, para o PDF e o Excel mostrarem números em vez de texto. Repete a
/// apuração das fórmulas do grupo; as faixas não alcançadas aparecem com parte da base zero.
/// </summary>
/// <param name="ValorExato">Soma dos produtos de cada faixa, sem truncar ou arredondar.</param>
/// <param name="Valor">Soma considerada: no INSS, a das faixas truncadas; no IRRF, a soma exata arredondada.</param>
public sealed record QuadroFaixasDto(IReadOnlyList<FaixaMemoriaDto> Faixas, decimal BaseCalculo, decimal ValorExato, decimal Valor);
