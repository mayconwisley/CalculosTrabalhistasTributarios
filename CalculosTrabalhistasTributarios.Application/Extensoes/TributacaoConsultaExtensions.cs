using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Extensoes;

public static class TributacaoConsultaExtensions
{
    /// <summary>Tabelas de INSS, IRRF e demais parâmetros vigentes na competência, prontas para a apuração.</summary>
    public static async Task<Result<TabelasDaCompetencia>> ObterTabelasAsync(this ITributacaoConsulta consulta, DateOnly competencia, CancellationToken cancellationToken) =>
        (await consulta.ObterPerfilAsync(competencia, cancellationToken)).Map(perfil => new TabelasDaCompetencia(competencia, perfil));
}
