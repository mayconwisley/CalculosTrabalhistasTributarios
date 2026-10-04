using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using System.Globalization;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>
/// Confere o desconto mínimo com a redação em vigor da lei. Ela vale desde o início da sua vigência até hoje: os registros
/// desse período com outro valor são corrigidos, e os de competências futuras, cadastrados pelo usuário, ficam como estão.
/// </summary>
public sealed class AtualizadorDescontoMinimo(
    Func<HttpClient> criarHttpClient,
    IEnumerable<IFonteTabela<DescontoMinimoPublicado>> fontes,
    BancoTributario banco,
    ICacheTabelasTributarias cache)
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly IFonteTabela<DescontoMinimoPublicado>[] _fontes = fontes.ToArray();

    public Uri FonteOficial => _fontes.First(fonte => fonte.Oficial).Endereco;

    public async Task<AtualizacaoTabelaResultado> AtualizarAsync(CancellationToken cancellationToken)
    {
        var escolha = await ConsultaDeFontes.EscolherAsync(_fontes, criarHttpClient, "desconto mínimo", cancellationToken);
        var publicado = escolha.Tabela;
        var vigencia = publicado.Competencia;
        var mesAtual = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);

        await using var conexao = await banco.AbrirAsync(cancellationToken);
        await using var transacao = conexao.BeginTransaction();
        var registros = await conexao.ListarAsync(transacao, "SELECT Id, Competencia, Valor FROM DescontoMinimo ORDER BY Competencia, Id",
            leitor => new Registro(leitor.GetInt32(0), DateOnly.FromDateTime(leitor.GetDateTime(1)), Math.Round(leitor.GetDecimal(2), 2)), cancellationToken);

        var gravados = 0;
        foreach (var registro in registros.Where(registro => registro.Competencia >= vigencia && registro.Competencia <= mesAtual && registro.Valor != publicado.Valor))
        {
            await conexao.ExecutarAsync(transacao, "UPDATE DescontoMinimo SET Valor = $valor WHERE Id = $id", cancellationToken, ("$valor", (double)publicado.Valor), ("$id", registro.Id));
            gravados++;
        }

        // No início da vigência vale o último registro até ele. Se esse registro é anterior e tem outro valor, ou se a tabela está
        // vazia, a redação em vigor entra como um registro novo; sem registro até a vigência, a tabela começa depois dela.
        var emVigor = registros.LastOrDefault(registro => registro.Competencia <= vigencia);
        if (registros.Length == 0 || emVigor is not null && emVigor.Competencia < vigencia && emVigor.Valor != publicado.Valor)
        {
            await conexao.ExecutarAsync(transacao, "INSERT INTO DescontoMinimo (Competencia, Valor) VALUES ($competencia, $valor)", cancellationToken,
                ("$competencia", vigencia.ToDateTime(TimeOnly.MinValue)), ("$valor", (double)publicado.Valor));
            gravados++;
        }
        await transacao.CommitAsync(cancellationToken);
        if (gravados > 0)
            cache.Invalidar();

        var regra = $"O {publicado.Norma} dispensa a retenção do IRRF de até R$ {publicado.Valor.ToString("N2", Cultura)} desde {vigencia:MM/yyyy}.";
        return new AtualizacaoTabelaResultado(vigencia, gravados, escolha.Fontes, escolha.Oficial, [.. escolha.Observacoes, regra]);
    }

    private sealed record Registro(int Id, DateOnly Competencia, decimal Valor);
}
