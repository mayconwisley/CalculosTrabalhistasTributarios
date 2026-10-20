using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Resultado de uma das tabelas na atualização de todas; a falha de uma não impede as demais.</summary>
public sealed record AtualizacaoDeTabela(TipoTabelaTributaria Tipo, string Nome, Result<AtualizacaoTabelaResultado> Resultado);
