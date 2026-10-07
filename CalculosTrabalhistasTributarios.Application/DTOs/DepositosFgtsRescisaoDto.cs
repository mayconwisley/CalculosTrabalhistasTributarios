using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>FGTS devido por competência anterior ao desligamento, para preencher o histórico de depósitos da rescisão.</summary>
public sealed record DepositosFgtsRescisaoDto(IReadOnlyList<DepositoFgtsHistorico> Depositos, DateOnly? Desligamento);
