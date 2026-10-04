using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using Microsoft.Data.Sqlite;
using System.Globalization;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence;

/// <summary>
/// Histórico no mesmo banco das tabelas, que o instalador preserva nas atualizações. A tabela é criada no primeiro uso,
/// independentemente da versão das sementes, porque guarda dados do usuário e não valores publicados.
/// </summary>
public sealed class SqliteHistoricoCalculos(BancoTributario banco) : IHistoricoCalculos
{
    private const string FormatoData = "yyyy-MM-dd HH:mm:ss";
    private const string Colunas = "\"Id\", \"Tipo\", \"Calculadora\", \"Nome\", \"Dados\", \"CriadoEm\", \"AlteradoEm\"";
    private readonly SemaphoreSlim _criacao = new(1, 1);
    private bool _tabelaCriada;

    public async Task<IReadOnlyList<CalculoSalvoDto>> ListarAsync(CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(cancellationToken);
        return await conexao.ListarAsync(null, $"SELECT {Colunas} FROM \"CalculosSalvos\" ORDER BY \"AlteradoEm\" DESC, \"Id\" DESC", Ler, cancellationToken);
    }

    public async Task<CalculoSalvoDto?> ObterAsync(long id, CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(cancellationToken);
        var itens = await conexao.ListarAsync(null, $"SELECT {Colunas} FROM \"CalculosSalvos\" WHERE \"Id\" = $id", Ler, cancellationToken, ("$id", id));
        return itens.FirstOrDefault();
    }

    public async Task<long> SalvarAsync(long? id, string tipo, string calculadora, string nome, string dados, CancellationToken cancellationToken)
    {
        var agora = Agora();
        await using var conexao = await AbrirAsync(cancellationToken);
        if (id is { } existente
            && await conexao.ExecutarAsync(null, "UPDATE \"CalculosSalvos\" SET \"Tipo\" = $tipo, \"Calculadora\" = $calculadora, \"Nome\" = $nome, \"Dados\" = $dados, \"AlteradoEm\" = $agora WHERE \"Id\" = $id",
                cancellationToken, ("$tipo", tipo), ("$calculadora", calculadora), ("$nome", nome), ("$dados", dados), ("$agora", agora), ("$id", existente)) > 0)
            return existente;

        // Um cálculo excluído enquanto estava aberto volta como novo.
        await using var comando = conexao.CriarComando(null,
            "INSERT INTO \"CalculosSalvos\" (\"Tipo\", \"Calculadora\", \"Nome\", \"Dados\", \"CriadoEm\", \"AlteradoEm\") VALUES ($tipo, $calculadora, $nome, $dados, $agora, $agora); SELECT last_insert_rowid();",
            ("$tipo", tipo), ("$calculadora", calculadora), ("$nome", nome), ("$dados", dados), ("$agora", agora));
        return (long)(await comando.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<Result<long>> DuplicarAsync(long id, CancellationToken cancellationToken)
    {
        if (await ObterAsync(id, cancellationToken) is not { } original)
            return Erro.NaoEncontrado("O cálculo não está mais no histórico.");
        return await SalvarAsync(null, original.Tipo, original.Calculadora, $"{original.Nome} (cópia)", original.Dados, cancellationToken);
    }

    public async Task RenomearAsync(long id, string nome, CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(cancellationToken);
        await conexao.ExecutarAsync(null, "UPDATE \"CalculosSalvos\" SET \"Nome\" = $nome, \"AlteradoEm\" = $agora WHERE \"Id\" = $id", cancellationToken, ("$nome", nome), ("$agora", Agora()), ("$id", id));
    }

    public async Task ExcluirAsync(long id, CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(cancellationToken);
        await conexao.ExecutarAsync(null, "DELETE FROM \"CalculosSalvos\" WHERE \"Id\" = $id", cancellationToken, ("$id", id));
    }

    private async Task<SqliteConnection> AbrirAsync(CancellationToken cancellationToken)
    {
        var conexao = await banco.AbrirAsync(cancellationToken);
        if (_tabelaCriada)
            return conexao;

        await _criacao.WaitAsync(cancellationToken);
        try
        {
            await conexao.ExecutarAsync(null, """
                CREATE TABLE IF NOT EXISTS "CalculosSalvos" (
                    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "Tipo" TEXT NOT NULL,
                    "Calculadora" TEXT NOT NULL,
                    "Nome" TEXT NOT NULL,
                    "Dados" TEXT NOT NULL,
                    "CriadoEm" TEXT NOT NULL,
                    "AlteradoEm" TEXT NOT NULL
                )
                """, cancellationToken);
            _tabelaCriada = true;
        }
        finally
        {
            _criacao.Release();
        }
        return conexao;
    }

    private static string Agora() => DateTime.Now.ToString(FormatoData, CultureInfo.InvariantCulture);

    private static CalculoSalvoDto Ler(SqliteDataReader leitor) => new(
        leitor.GetInt64(0), leitor.GetString(1), leitor.GetString(2), leitor.GetString(3), leitor.GetString(4),
        DateTime.ParseExact(leitor.GetString(5), FormatoData, CultureInfo.InvariantCulture),
        DateTime.ParseExact(leitor.GetString(6), FormatoData, CultureInfo.InvariantCulture));
}
