using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// Parcela isenta da aposentadoria e da pensão, inclusive da previdência complementar, a partir do mês em que o beneficiário
/// completa 65 anos, além da faixa isenta da tabela (Lei 7.713/1988, art. 6º, XV). O valor de R$ 1.903,98, da alínea "h"
/// (Lei 13.149/2015), vale desde 04/2015 e continua nas tabelas de 2025 e 2026 da Receita Federal, embora a faixa isenta
/// da tabela mensal tenha subido. Por ser rendimento isento, sai da base mensal nas duas modalidades: o desconto
/// simplificado substitui só as demais deduções (Solução de Consulta Cosit 99.004/2026).
/// </summary>
public static class ParcelaIsentaAposentadoria
{
    private static readonly DateOnly Inicio = new(2015, 4, 1);

    public const decimal Valor = 1_903.98m;

    public static Result<decimal> Mensal(DateOnly competencia) => competencia >= Inicio
        ? Valor
        : Erro.Validacao($"Competência: a parcela isenta de R$ 1.903,98 dos aposentados com 65 anos ou mais vale desde 04/2015; informe uma competência a partir dela.");
}
