using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>Importa as faixas do seguro-desemprego da competência confirmada, substituindo as faixas dessa competência.</summary>
public sealed class AtualizadorSeguroDesemprego(
    Func<HttpClient> criarHttpClient,
    IEnumerable<IFonteTabela<TabelaSeguroDesempregoPublicada>> fontes,
    BancoTributario banco,
    ICacheTabelasTributarias cache)
{
    private readonly IFonteTabela<TabelaSeguroDesempregoPublicada>[] _fontes = fontes.ToArray();

    public Uri FonteOficial => _fontes.First(fonte => fonte.Oficial).Endereco;

    public async Task<AtualizacaoTabelaResultado> AtualizarAsync(CancellationToken cancellationToken)
    {
        var escolha = await ConsultaDeFontes.EscolherAsync(_fontes, criarHttpClient, "seguro-desemprego", cancellationToken);
        var tabela = escolha.Tabela;
        var competencia = tabela.Competencia.ToDateTime(TimeOnly.MinValue);

        await using var conexao = await banco.AbrirAsync(cancellationToken);
        await using var transacao = conexao.BeginTransaction();
        await conexao.ExecutarAsync(transacao, "DELETE FROM SeguroDesemprego WHERE Competencia = $competencia", cancellationToken, ("$competencia", competencia));
        for (var indice = 0; indice < tabela.Faixas.Count; indice++)
        {
            var faixa = tabela.Faixas[indice];
            await conexao.ExecutarAsync(transacao, "INSERT INTO SeguroDesemprego (Competencia, Faixa, Valor, Porcentagem, ValorFixo) VALUES ($competencia, $faixa, $valor, $porcentagem, $fixo)", cancellationToken,
                ("$competencia", competencia), ("$faixa", indice + 1), ("$valor", (double)faixa.LimiteMedia), ("$porcentagem", (double)faixa.Percentual), ("$fixo", (double)faixa.ValorFixo));
        }
        await transacao.CommitAsync(cancellationToken);
        cache.Invalidar();

        return new AtualizacaoTabelaResultado(tabela.Competencia, tabela.Faixas.Count, escolha.Fontes, escolha.Oficial, escolha.Observacoes);
    }
}
