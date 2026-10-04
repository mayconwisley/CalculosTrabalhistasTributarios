using Microsoft.Data.Sqlite;
using static CalculosTrabalhistasTributarios.Infrastructure.Persistence.Inicializacao.SementesTributarias;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence.Inicializacao;

/// <summary>Grava as sementes que faltam no banco, sem alterar os registros existentes nem as manutenções locais.</summary>
internal static class CargaDeSementes
{
    internal static async Task InserirFaixasInssAusentesAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken)
    {
        var existentes = (await conexao.ListarAsync(transacao, "SELECT Competencia, Faixa FROM Inss", leitor => (leitor.GetDateTime(0), leitor.GetInt32(1)), cancellationToken)).ToHashSet();
        foreach (var item in FaixasInss.Where(item => !existentes.Contains((item.Competencia, item.Faixa))))
            await conexao.ExecutarAsync(transacao, "INSERT INTO Inss (Competencia, Faixa, Valor, Porcentagem) VALUES ($competencia, $faixa, $valor, $porcentagem)", cancellationToken,
                ("$competencia", item.Competencia), ("$faixa", item.Faixa), ("$valor", item.Limite), ("$porcentagem", item.Aliquota));
    }

    internal static async Task InserirFaixasIrrfAusentesAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken)
    {
        var existentes = (await conexao.ListarAsync(transacao, "SELECT Competencia, Faixa FROM Irrf", leitor => (leitor.GetDateTime(0), leitor.GetInt32(1)), cancellationToken)).ToHashSet();
        foreach (var item in FaixasIrrf.Where(item => !existentes.Contains((item.Competencia, item.Faixa))))
            await conexao.ExecutarAsync(transacao, "INSERT INTO Irrf (Competencia, Faixa, Valor, Porcentagem, Deducao) VALUES ($competencia, $faixa, $valor, $porcentagem, $deducao)", cancellationToken,
                ("$competencia", item.Competencia), ("$faixa", item.Faixa), ("$valor", item.Limite), ("$porcentagem", item.Aliquota), ("$deducao", item.Deducao));
    }

    internal static async Task InserirFaixasSeguroDesempregoAusentesAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken)
    {
        var existentes = (await conexao.ListarAsync(transacao, "SELECT Competencia, Faixa FROM SeguroDesemprego", leitor => (leitor.GetDateTime(0), leitor.GetInt32(1)), cancellationToken)).ToHashSet();
        foreach (var item in FaixasSeguroDesemprego.Where(item => !existentes.Contains((item.Competencia, item.Faixa))))
            await conexao.ExecutarAsync(transacao, "INSERT INTO SeguroDesemprego (Competencia, Faixa, Valor, Porcentagem, ValorFixo) VALUES ($competencia, $faixa, $valor, $porcentagem, $fixo)", cancellationToken,
                ("$competencia", item.Competencia), ("$faixa", item.Faixa), ("$valor", item.Limite), ("$porcentagem", item.Aliquota), ("$fixo", item.Deducao));
    }

    internal static async Task InserirFaixasPlrAusentesAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken)
    {
        var existentes = (await conexao.ListarAsync(transacao, "SELECT Competencia, Faixa FROM Plr", leitor => (leitor.GetDateTime(0), leitor.GetInt32(1)), cancellationToken)).ToHashSet();
        foreach (var item in FaixasPlr.Where(item => !existentes.Contains((item.Competencia, item.Faixa))))
            await conexao.ExecutarAsync(transacao, "INSERT INTO Plr (Competencia, Faixa, Valor, Porcentagem, Deducao) VALUES ($competencia, $faixa, $valor, $porcentagem, $deducao)", cancellationToken,
                ("$competencia", item.Competencia), ("$faixa", item.Faixa), ("$valor", item.Limite), ("$porcentagem", item.Aliquota), ("$deducao", item.Deducao));
    }

    internal static async Task InserirFaixasSalarioFamiliaAusentesAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken)
    {
        var existentes = (await conexao.ListarAsync(transacao, "SELECT Competencia, Faixa FROM SalarioFamilia", leitor => (leitor.GetDateTime(0), leitor.GetInt32(1)), cancellationToken)).ToHashSet();
        foreach (var item in FaixasSalarioFamilia.Where(item => !existentes.Contains((item.Competencia, item.Faixa))))
            await conexao.ExecutarAsync(transacao, "INSERT INTO SalarioFamilia (Competencia, Faixa, LimiteRemuneracao, Cota) VALUES ($competencia, $faixa, $limite, $cota)", cancellationToken,
                ("$competencia", item.Competencia), ("$faixa", item.Faixa), ("$limite", item.LimiteRemuneracao), ("$cota", item.Cota));
    }

    internal static async Task InserirParametrosAusentesAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken)
    {
        await InserirParametrosAusentesAsync(conexao, transacao, "Dependente", DeducoesPorDependente, cancellationToken);
        await InserirParametrosAusentesAsync(conexao, transacao, "Simplificado", DescontosSimplificados, cancellationToken);
        await InserirParametrosAusentesAsync(conexao, transacao, "DescontoMinimo", DescontosMinimos, cancellationToken);
        await InserirParametrosAusentesAsync(conexao, transacao, "SalarioMinimo", SalariosMinimos, cancellationToken);
        await InserirParametrosAusentesAsync(conexao, transacao, "Inpc", VariacoesInpc, cancellationToken);
        await InserirParametrosAusentesAsync(conexao, transacao, "Ipca", VariacoesIpca, cancellationToken);
        await InserirParametrosAusentesAsync(conexao, transacao, "TaxaLegal", TaxasLegais, cancellationToken);
        await InserirParametrosAusentesAsync(conexao, transacao, "Selic", TaxasSelic, cancellationToken);
        await InserirParametrosAusentesAsync(conexao, transacao, "IpcaE", VariacoesIpcaE, cancellationToken);
        await InserirParametrosAusentesAsync(conexao, transacao, "Tr", TaxasTr, cancellationToken);
    }

    private static async Task InserirParametrosAusentesAsync(SqliteConnection conexao, SqliteTransaction transacao, string tabela, SementeParametro[] sementes, CancellationToken cancellationToken)
    {
        var existentes = (await conexao.ListarAsync(transacao, $"SELECT Competencia FROM {tabela}", leitor => leitor.GetDateTime(0), cancellationToken)).ToHashSet();
        foreach (var item in sementes.Where(item => !existentes.Contains(item.Competencia)))
            await conexao.ExecutarAsync(transacao, $"INSERT INTO {tabela} (Competencia, Valor) VALUES ($competencia, $valor)", cancellationToken,
                ("$competencia", item.Competencia), ("$valor", item.Valor));
    }
}
