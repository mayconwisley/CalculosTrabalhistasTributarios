using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Salarios">Salários dos últimos meses antes da dispensa, do mais recente ao mais antigo; os zerados ficam fora da média.</param>
/// <param name="MesesTrabalhados">Meses com salário nos 36 meses anteriores à dispensa, contando a fração de 15 dias ou mais.</param>
/// <param name="Domestico">Empregado doméstico: um salário mínimo, em até 3 parcelas, com 15 meses nos últimos 24 (LC 150/2015).</param>
public sealed record SimularSeguroDesempregoRequest(DateOnly Dispensa, IReadOnlyList<decimal> Salarios, SolicitacaoSeguroDesemprego Solicitacao, int MesesTrabalhados, bool Domestico = false);
