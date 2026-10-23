using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>Importa o limite de remuneração e a cota do salário-família da competência confirmada.</summary>
public sealed class AtualizadorSalarioFamilia(
    Func<HttpClient> criarHttpClient,
    IEnumerable<IFonteTabela<TabelaSalarioFamiliaPublicada>> fontes,
    BancoTributario banco,
    ICacheTabelasTributarias cache)
{
    private readonly IFonteTabela<TabelaSalarioFamiliaPublicada>[] _fontes = fontes.ToArray();

    public Uri FonteOficial => _fontes.First(fonte => fonte.Oficial).Endereco;

    public async Task<AtualizacaoTabelaResultado> AtualizarAsync(CancellationToken cancellationToken)
    {
        var escolha = await ConsultaDeFontes.EscolherAsync(_fontes, criarHttpClient, "salário-família", cancellationToken);
        var tabela = escolha.Tabela;
        var competencia = tabela.Competencia.ToDateTime(TimeOnly.MinValue);

        await using var conexao = await banco.AbrirAsync(cancellationToken);
        await using var transacao = conexao.BeginTransaction();
        await conexao.ExecutarAsync(transacao, "DELETE FROM SalarioFamilia WHERE Competencia = $competencia", cancellationToken, ("$competencia", competencia));
        for (var indice = 0; indice < tabela.Faixas.Count; indice++)
        {
            var faixa = tabela.Faixas[indice];
            await conexao.ExecutarAsync(transacao, "INSERT INTO SalarioFamilia (Competencia, Faixa, LimiteRemuneracao, Cota) VALUES ($competencia, $faixa, $limite, $cota)", cancellationToken,
                ("$competencia", competencia), ("$faixa", indice + 1), ("$limite", (double)faixa.LimiteRemuneracao), ("$cota", (double)faixa.Cota));
        }
        await transacao.CommitAsync(cancellationToken);
        cache.Invalidar();

        return new AtualizacaoTabelaResultado(tabela.Competencia, tabela.Faixas.Count, escolha.Fontes, escolha.Oficial, escolha.Observacoes);
    }
}
