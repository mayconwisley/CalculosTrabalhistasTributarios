using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence;

/// <summary>Lê as séries direto do banco: são pequenas e só a pensão em atraso e os débitos judiciais as usam.</summary>
public sealed class SqliteIndicesEconomicos(BancoTributario banco) : IIndicesEconomicos
{
    public static string Tabela(IndiceEconomico indice) => indice switch
    {
        IndiceEconomico.Inpc => "Inpc",
        IndiceEconomico.Ipca => "Ipca",
        IndiceEconomico.Selic => "Selic",
        IndiceEconomico.IpcaE => "IpcaE",
        IndiceEconomico.Tr => "Tr",
        _ => "TaxaLegal"
    };

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> ObterAsync(IndiceEconomico indice, CancellationToken cancellationToken)
    {
        await using var conexao = await banco.AbrirAsync(cancellationToken);
        var registros = await conexao.ListarAsync(null, $"SELECT Competencia, Valor FROM {Tabela(indice)} ORDER BY Id",
            leitor => (Competencia: DateOnly.FromDateTime(leitor.GetDateTime(0)), Valor: leitor.GetDecimal(1)), cancellationToken);
        // Um valor por mês; se o mesmo mês foi cadastrado duas vezes, vale o registro mais recente.
        var serie = new Dictionary<DateOnly, decimal>();
        foreach (var (competencia, valor) in registros)
            serie[new DateOnly(competencia.Year, competencia.Month, 1)] = valor;
        return serie;
    }
}
