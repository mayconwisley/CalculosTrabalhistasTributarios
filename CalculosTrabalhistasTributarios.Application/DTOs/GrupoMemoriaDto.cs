namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record GrupoMemoriaDto(string Titulo, string Destaque, IReadOnlyList<FormulaDto> Formulas);
