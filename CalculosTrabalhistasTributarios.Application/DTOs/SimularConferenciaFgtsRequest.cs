using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Desligamento">Data de desligamento, exigida quando há competência rescisória; nula sem rescisão.</param>
public sealed record SimularConferenciaFgtsRequest(CategoriaFgts Categoria, IReadOnlyList<LancamentoFgts> Lancamentos, DateOnly? Desligamento);
