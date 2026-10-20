namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Andamento da atualização de todas as tabelas: quantas já foram consultadas e qual está sendo consultada.</summary>
/// <param name="Concluidas">Tabelas já consultadas, com sucesso ou não.</param>
/// <param name="Total">Tabelas da atualização.</param>
/// <param name="TabelaAtual">Nome da tabela em consulta; vazio quando todas terminaram.</param>
public sealed record ProgressoAtualizacaoTabelas(int Concluidas, int Total, string TabelaAtual);
