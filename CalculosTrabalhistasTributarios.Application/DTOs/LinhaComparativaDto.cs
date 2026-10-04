namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Destaque">Linha em negrito, como a do resultado principal.</param>
public sealed record LinhaComparativaDto(string Descricao, IReadOnlyList<string> Valores, bool Destaque = false);
