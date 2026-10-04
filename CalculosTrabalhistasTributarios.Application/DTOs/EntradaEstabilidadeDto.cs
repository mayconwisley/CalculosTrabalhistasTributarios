namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record EntradaEstabilidadeDto(decimal MediaRemuneratoria, int DiasBase, DateOnly Demissao, DateOnly FimEstabilidade, decimal Complementos);
