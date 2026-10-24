using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence;

/// <summary>
/// Histórico no mesmo banco das tabelas, que o instalador preserva nas atualizações. A tabela é criada no primeiro uso,
/// independentemente da versão das sementes, porque guarda dados do usuário e não valores publicados.
/// </summary>
public sealed class SqliteHistoricoCalculos(BancoTributario banco) : IHistoricoCalculos, IConsultaHistoricoCalculos
{
    private const string FormatoData = "yyyy-MM-dd HH:mm:ss";
    private const string TipoSimulacaoTributariaAnterior = "SimulacaoTributaria";
    private const string TipoSimulacaoTributaria = "Calculadora.SimulacaoTributaria";
    private const string Colunas ="\"Id\", \"Tipo\", \"Calculadora\", \"Nome\", \"Dados\", \"CriadoEm\", \"AlteradoEm\"";
    private readonly SemaphoreSlim _criacao = new(1, 1);
    private bool _tabelaCriada;

    public async Task<IReadOnlyList<CalculoSalvoDto>> ListarAsync(CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(cancellationToken);
        return await conexao.ListarAsync(null, $"SELECT {Colunas} FROM \"CalculosSalvos\" ORDER BY \"AlteradoEm\" DESC, \"Id\" DESC", Ler, cancellationToken);
    }

    // Microsoft.Data.Sqlite executa parte do I/O sincronamente; a consulta inteira fica fora da UI.
    public Task<PaginaHistoricoDto> ConsultarAsync(ConsultaHistoricoDto consulta, CancellationToken cancellationToken) =>
        Task.Run(() => ConsultarPaginaAsync(consulta, cancellationToken), cancellationToken);

    private async Task<PaginaHistoricoDto> ConsultarPaginaAsync(ConsultaHistoricoDto consulta, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(consulta.Deslocamento);
        if (consulta.Limite is < 1 or > 100 || !Enum.IsDefined(consulta.Ordem))
            throw new ArgumentOutOfRangeException(nameof(consulta));
        await using var conexao = await AbrirAsync(cancellationToken);
        var comparacao = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        conexao.CreateFunction<string, string, bool>("contem_historico", (texto, busca) =>
            comparacao.IndexOf(texto, busca, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0, isDeterministic: true);
        conexao.CreateCollation("historico_ptbr", (a, b) => comparacao.Compare(a, b, CompareOptions.IgnoreCase));
        using var transacao = conexao.BeginTransaction();
        const string filtro = """
            WHERE ($busca = '' OR contem_historico("Nome", $busca) OR contem_historico("Calculadora", $busca))
              AND ($calculadora = '' OR "Calculadora" = $calculadora)
              AND ($desde = '' OR "AlteradoEm" >= $desde)
              AND ($ate = '' OR "AlteradoEm" < $ate)
            """;
        (string Nome, object Valor)[] parametros = [("$busca", consulta.Busca?.Trim() ?? ""), ("$calculadora", consulta.Calculadora ?? ""),
            ("$desde", consulta.Desde?.ToString(FormatoData, CultureInfo.InvariantCulture) ?? ""),
            ("$ate", consulta.AteExclusivo?.ToString(FormatoData, CultureInfo.InvariantCulture) ?? "")];
        var totais = await conexao.ListarAsync(transacao, $"SELECT COUNT(*) FROM CalculosSalvos {filtro}", r => r.GetInt32(0), cancellationToken, parametros);
        var geral = await conexao.ListarAsync(transacao, "SELECT COUNT(*) FROM CalculosSalvos", r => r.GetInt32(0), cancellationToken);
        var calculadoras = await conexao.ListarAsync(transacao, "SELECT DISTINCT Calculadora FROM CalculosSalvos ORDER BY Calculadora COLLATE historico_ptbr", r => r.GetString(0), cancellationToken);
        var ordem = consulta.Ordem switch
        {
            OrdemHistorico.MaisAntigos => "AlteradoEm ASC, Id ASC",
            OrdemHistorico.Nome => "Nome COLLATE historico_ptbr ASC, Id ASC",
            _ => "AlteradoEm DESC, Id DESC"
        };
        var itens = await conexao.ListarAsync(transacao, $"""
            SELECT Id, Tipo, Calculadora, Nome, CriadoEm, AlteradoEm FROM CalculosSalvos
            {filtro} ORDER BY {ordem} LIMIT $limite OFFSET $deslocamento
            """, r => new ResumoCalculoSalvoDto(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3),
                DateTime.ParseExact(r.GetString(4), FormatoData, CultureInfo.InvariantCulture),
                DateTime.ParseExact(r.GetString(5), FormatoData, CultureInfo.InvariantCulture)), cancellationToken,
            [.. parametros, ("$limite", consulta.Limite), ("$deslocamento", consulta.Deslocamento)]);
        cancellationToken.ThrowIfCancellationRequested();
        transacao.Commit();
        return new(itens, totais[0], geral[0], calculadoras);
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
            await conexao.ExecutarAsync(null, "CREATE INDEX IF NOT EXISTS IX_CalculosSalvos_AlteradoEm_Id ON CalculosSalvos (AlteradoEm DESC, Id DESC)", cancellationToken);
            // A simulação tributária tinha janela própria e passou para a janela padrão das calculadoras: os cálculos
            // salvos antes passam a abrir, recalcular e comparar como os da nova calculadora. O formulário é o mesmo,
            // e a calculadora lê os nomes antigos dos campos.
            await conexao.ExecutarAsync(null, "UPDATE \"CalculosSalvos\" SET \"Tipo\" = $novo WHERE \"Tipo\" = $antigo", cancellationToken,
                ("$novo", TipoSimulacaoTributaria), ("$antigo", TipoSimulacaoTributariaAnterior));
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
