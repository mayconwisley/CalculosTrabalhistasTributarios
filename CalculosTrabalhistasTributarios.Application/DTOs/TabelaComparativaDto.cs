namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Valores lado a lado, como a situação atual e a proposta; cada linha tem um valor por coluna.</summary>
public sealed record TabelaComparativaDto(string Titulo, IReadOnlyList<string> Colunas, IReadOnlyList<LinhaComparativaDto> Linhas);
