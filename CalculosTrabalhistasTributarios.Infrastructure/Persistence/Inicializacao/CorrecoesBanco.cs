using Microsoft.Data.Sqlite;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence.Inicializacao;

/// <summary>Correções de valores gravados por versões anteriores com erro.</summary>
internal static class CorrecoesBanco
{
    // As versões anteriores gravavam R$ 0,00, mas a dispensa de retenção do IRRF de até R$ 10,00 vale desde 1997.
    internal static Task CorrigirDescontoMinimoAsync(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken) =>
        conexao.ExecutarAsync(transacao, """UPDATE "DescontoMinimo" SET "Valor" = 10 WHERE "Valor" = 0;""", cancellationToken);

    internal static Task CorrigirOuInserirReducaoMensal2026Async(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken) => conexao.ExecutarAsync(transacao, """
        INSERT INTO "ReducaoMensalIrrf" ("Competencia", "Faixa", "LimiteRendimentos", "Multiplicador", "ValorBase")
        SELECT '2026-01-01 00:00:00', 1, 5000.00, 0.000000, 312.89
        WHERE NOT EXISTS (SELECT 1 FROM "ReducaoMensalIrrf" WHERE "Competencia" = '2026-01-01 00:00:00' AND "Faixa" = 1);

        INSERT INTO "ReducaoMensalIrrf" ("Competencia", "Faixa", "LimiteRendimentos", "Multiplicador", "ValorBase")
        SELECT '2026-01-01 00:00:00', 2, 7350.00, 0.133145, 978.62
        WHERE NOT EXISTS (SELECT 1 FROM "ReducaoMensalIrrf" WHERE "Competencia" = '2026-01-01 00:00:00' AND "Faixa" = 2);

        UPDATE "ReducaoMensalIrrf"
        SET "ValorBase" = 978.62
        WHERE "Competencia" = '2026-01-01 00:00:00'
          AND "Faixa" = 2
          AND "LimiteRendimentos" = 7350.00
          AND "Multiplicador" = 0.133145
          AND "ValorBase" = 7350.00;
        """, cancellationToken);

    /// <summary>
    /// Corrige os limites das faixas 2 e 3 de 01/2022 gravados por sementes antigas (Portaria Interministerial MTP/ME nº 12/2022).
    /// Só altera o valor quando ele ainda é exatamente o errado, preservando edições manuais.
    /// </summary>
    internal static Task CorrigirFaixasInss2022Async(SqliteConnection conexao, SqliteTransaction transacao, CancellationToken cancellationToken) => conexao.ExecutarAsync(transacao, """
        UPDATE "Inss"
        SET "Valor" = 2427.35
        WHERE "Competencia" = '2022-01-01 00:00:00'
          AND "Faixa" = 2
          AND "Valor" = 2452.67;

        UPDATE "Inss"
        SET "Valor" = 3641.03
        WHERE "Competencia" = '2022-01-01 00:00:00'
          AND "Faixa" = 3
          AND "Valor" = 3679.00;
        """, cancellationToken);
}
