using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>Importa o salário mínimo da competência confirmada.</summary>
public sealed class AtualizadorSalarioMinimo(
    Func<HttpClient> criarHttpClient,
    IEnumerable<IFonteTabela<SalarioMinimoPublicado>> fontes,
    BancoTributario banco,
    ICacheTabelasTributarias cache)
{
    private readonly IFonteTabela<SalarioMinimoPublicado>[] _fontes = fontes.ToArray();

    public Uri FonteOficial => _fontes.First(fonte => fonte.Oficial).Endereco;

    public async Task<AtualizacaoTabelaResultado> AtualizarAsync(CancellationToken cancellationToken)
    {
        var escolha = await ConsultaDeFontes.EscolherAsync(_fontes, criarHttpClient, "salário mínimo", cancellationToken);
        var salario = escolha.Tabela;
        var competencia = salario.Competencia.ToDateTime(TimeOnly.MinValue);

        await using var conexao = await banco.AbrirAsync(cancellationToken);
        await using var transacao = conexao.BeginTransaction();
        var ids = await conexao.ListarAsync(transacao, "SELECT Id FROM SalarioMinimo WHERE Competencia = $competencia LIMIT 2", leitor => leitor.GetInt32(0), cancellationToken, ("$competencia", competencia));
        if (ids.Length > 1)
            throw new InvalidOperationException($"Há mais de um salário mínimo cadastrado para a competência {competencia:MM/yyyy}. Nenhum dado foi alterado.");
        if (ids.Length == 0)
            await conexao.ExecutarAsync(transacao, "INSERT INTO SalarioMinimo (Competencia, Valor) VALUES ($competencia, $valor)", cancellationToken, ("$competencia", competencia), ("$valor", (double)salario.Valor));
        else
            await conexao.ExecutarAsync(transacao, "UPDATE SalarioMinimo SET Valor = $valor WHERE Id = $id", cancellationToken, ("$valor", (double)salario.Valor), ("$id", ids[0]));
        await transacao.CommitAsync(cancellationToken);
        cache.Invalidar();

        return new AtualizacaoTabelaResultado(salario.Competencia, 1, escolha.Fontes, escolha.Oficial, escolha.Observacoes);
    }
}
