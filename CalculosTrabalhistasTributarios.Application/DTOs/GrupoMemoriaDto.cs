namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Quadro">Faixas do INSS ou do IRRF em tabela, para o PDF e o Excel; nulo nos demais grupos.</param>
public sealed record GrupoMemoriaDto(string Titulo, string Destaque, IReadOnlyList<FormulaDto> Formulas, QuadroFaixasDto? Quadro = null);
