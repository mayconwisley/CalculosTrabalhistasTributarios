using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public sealed record VerbasVariaveisDoMes(
    DateOnly Competencia, decimal Comissoes, decimal Dsr, decimal HorasExtras, decimal Adicionais, decimal Outras);

public sealed record ApuracaoMediaVerbasVariaveis(
    IReadOnlyList<VerbasVariaveisDoMes> Meses, int Divisor,
    decimal TotalComissoes, decimal TotalDsr, decimal TotalHorasExtras, decimal TotalAdicionais, decimal TotalOutras)
{
    public decimal MediaComissoes => Arredondar(TotalComissoes / Divisor);
    public decimal MediaDsr => Arredondar(TotalDsr / Divisor);
    public decimal MediaHorasExtras => Arredondar(TotalHorasExtras / Divisor);
    public decimal MediaAdicionais => Arredondar(TotalAdicionais / Divisor);
    public decimal MediaOutras => Arredondar(TotalOutras / Divisor);
    public decimal Total => TotalComissoes + TotalDsr + TotalHorasExtras + TotalAdicionais + TotalOutras;
    public decimal MediaTotal => Arredondar(Total / Divisor);

    private static decimal Arredondar(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
}

public static class CalculadoraMediaVerbasVariaveis
{
    public static Result<ApuracaoMediaVerbasVariaveis> Calcular(IReadOnlyList<VerbasVariaveisDoMes>? meses, int divisor)
    {
        if (meses is null || meses.Count == 0 || meses.Count > 24)
            return Erro.Validacao("Informe de 1 a 24 competências para a média.");
        if (divisor < 1 || divisor > 24)
            return Erro.Validacao("O divisor da média deve ficar entre 1 e 24 meses.");
        var competencias = new HashSet<DateOnly>();
        foreach (var mes in meses)
        {
            if (mes.Competencia.Day != 1 || !competencias.Add(mes.Competencia))
                return Erro.Validacao("Cada competência deve aparecer uma única vez no formato MM/AAAA.");
            if (new[] { mes.Comissoes, mes.Dsr, mes.HorasExtras, mes.Adicionais, mes.Outras }
                .Any(valor => valor < 0m || valor > 1_000_000_000m || decimal.Round(valor, 2) != valor))
                return Erro.Validacao($"Revise os valores de {mes.Competencia:MM/yyyy}: use valores não negativos em reais e centavos.");
        }
        return new ApuracaoMediaVerbasVariaveis(meses.OrderBy(mes => mes.Competencia).ToArray(), divisor,
            meses.Sum(mes => mes.Comissoes), meses.Sum(mes => mes.Dsr), meses.Sum(mes => mes.HorasExtras),
            meses.Sum(mes => mes.Adicionais), meses.Sum(mes => mes.Outras));
    }
}
