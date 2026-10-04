using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence.Inicializacao;
using Microsoft.Data.Sqlite;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence;

/// <summary>
/// Popula de forma idempotente as tabelas históricas do INSS e do IRRF.
/// Os registros existentes são preservados para não sobrescrever manutenções locais.
/// A carga só é executada quando o banco ainda não está na <see cref="VersaoSementes"/> atual.
/// </summary>
public sealed class InicializadorBancoTributario(BancoTributario banco) : IInicializadorBancoTributario
{
    /// <summary>Incremente ao alterar as sementes ou os scripts, para que bancos existentes recebam a nova carga.</summary>
    private const int VersaoSementes = 7;

    public async Task InicializarAsync(CancellationToken cancellationToken)
    {
        await using var conexao = await banco.AbrirAsync(cancellationToken);
        if (await ObterVersaoBancoAsync(conexao, cancellationToken) >= VersaoSementes)
            return;

        // Uma única transação: ou a carga inteira é gravada junto com a versão, ou nada muda.
        await using var transacao = conexao.BeginTransaction();
        await EsquemaBanco.CriarTabelaReducaoMensalAsync(conexao, transacao, cancellationToken);
        await EsquemaBanco.CriarTabelasDasCalculadorasAsync(conexao, transacao, cancellationToken);
        await EsquemaBanco.CriarTabelasDeIndicesAsync(conexao, transacao, cancellationToken);
        await EsquemaBanco.CriarTabelaSeguroDesempregoAsync(conexao, transacao, cancellationToken);
        await CorrecoesBanco.CorrigirOuInserirReducaoMensal2026Async(conexao, transacao, cancellationToken);
        await CorrecoesBanco.CorrigirFaixasInss2022Async(conexao, transacao, cancellationToken);
        await CorrecoesBanco.CorrigirDescontoMinimoAsync(conexao, transacao, cancellationToken);
        await CargaDeSementes.InserirFaixasInssAusentesAsync(conexao, transacao, cancellationToken);
        await CargaDeSementes.InserirFaixasIrrfAusentesAsync(conexao, transacao, cancellationToken);
        await CargaDeSementes.InserirFaixasPlrAusentesAsync(conexao, transacao, cancellationToken);
        await CargaDeSementes.InserirFaixasSalarioFamiliaAusentesAsync(conexao, transacao, cancellationToken);
        await CargaDeSementes.InserirFaixasSeguroDesempregoAusentesAsync(conexao, transacao, cancellationToken);
        await CargaDeSementes.InserirParametrosAusentesAsync(conexao, transacao, cancellationToken);
        await conexao.ExecutarAsync(transacao, $"PRAGMA user_version = {VersaoSementes}", cancellationToken);
        await transacao.CommitAsync(cancellationToken);
    }

    private static async Task<int> ObterVersaoBancoAsync(SqliteConnection conexao, CancellationToken cancellationToken)
    {
        await using var comando = conexao.CriarComando(null, "PRAGMA user_version");
        return Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken));
    }
}
