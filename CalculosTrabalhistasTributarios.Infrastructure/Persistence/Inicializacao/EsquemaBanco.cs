using Microsoft.Data.Sqlite;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence.Inicializacao;

/// <summary>Criação das tabelas que não existiam nas primeiras versões do banco.</summary>
internal static class EsquemaBanco
{
    internal static Task CriarTabelaReducaoMensalAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken) => conexao.ExecutarAsync(transacao, """
        CREATE TABLE IF NOT EXISTS "ReducaoMensalIrrf" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_ReducaoMensalIrrf" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Faixa" INTEGER NOT NULL,
            "LimiteRendimentos" REAL NOT NULL,
            "Multiplicador" REAL NOT NULL,
            "ValorBase" REAL NOT NULL
        );
        """, cancellationToken);

    internal static Task CriarTabelasDasCalculadorasAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken) => conexao.ExecutarAsync(transacao, """
        CREATE TABLE IF NOT EXISTS "SalarioMinimo" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_SalarioMinimo" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Valor" REAL NOT NULL
        );

        CREATE TABLE IF NOT EXISTS "SalarioFamilia" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_SalarioFamilia" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Faixa" INTEGER NOT NULL,
            "LimiteRemuneracao" REAL NOT NULL,
            "Cota" REAL NOT NULL
        );

        CREATE TABLE IF NOT EXISTS "Plr" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Plr" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Faixa" INTEGER NOT NULL,
            "Valor" REAL NOT NULL,
            "Porcentagem" REAL NOT NULL,
            "Deducao" REAL NOT NULL
        );
        """, cancellationToken);

    internal static Task CriarTabelasDeIndicesAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken) => conexao.ExecutarAsync(transacao, """
        CREATE TABLE IF NOT EXISTS "Inpc" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Inpc" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Valor" REAL NOT NULL
        );

        CREATE TABLE IF NOT EXISTS "Ipca" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Ipca" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Valor" REAL NOT NULL
        );

        CREATE TABLE IF NOT EXISTS "TaxaLegal" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_TaxaLegal" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Valor" REAL NOT NULL
        );

        CREATE TABLE IF NOT EXISTS "Selic" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Selic" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Valor" REAL NOT NULL
        );

        CREATE TABLE IF NOT EXISTS "IpcaE" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_IpcaE" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Valor" REAL NOT NULL
        );

        CREATE TABLE IF NOT EXISTS "Tr" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Tr" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Valor" REAL NOT NULL
        );
        """, cancellationToken);

    internal static Task CriarTabelaSeguroDesempregoAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken) => conexao.ExecutarAsync(transacao, """
        CREATE TABLE IF NOT EXISTS "SeguroDesemprego" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_SeguroDesemprego" PRIMARY KEY AUTOINCREMENT,
            "Competencia" TEXT NOT NULL,
            "Faixa" INTEGER NOT NULL,
            "Valor" REAL NOT NULL,
            "Porcentagem" REAL NOT NULL,
            "ValorFixo" REAL NOT NULL
        );
        """, cancellationToken);
}
