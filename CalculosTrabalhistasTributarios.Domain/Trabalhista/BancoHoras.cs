using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public enum RegimeBancoHoras { MesmoMes, AcordoIndividualEscrito, AcordoColetivo }
public enum TipoLancamentoBancoHoras { Credito, Compensacao }
public enum SituacaoBancoHoras { Acompanhamento, Fechamento, Rescisao }

public sealed record LancamentoBancoHoras(DateOnly Data, TipoLancamentoBancoHoras Tipo, int Minutos, string Descricao);

public sealed record MovimentoBancoHoras(LancamentoBancoHoras Lancamento, int SaldoAposLancamento);

public sealed record ApuracaoBancoHoras(IReadOnlyList<MovimentoBancoHoras> Movimentos, int MinutosCreditados,
    int MinutosCompensados, int SaldoMinutos, decimal ValorHora, decimal ValorQuitacao, SituacaoBancoHoras Situacao)
{
    public int SaldoCredor => Math.Max(0, SaldoMinutos);
    public int SaldoDevedor => Math.Max(0, -SaldoMinutos);
    public decimal ValorAPagar => Situacao == SituacaoBancoHoras.Acompanhamento ? 0m : ValorQuitacao;
}

public static class CalculadoraBancoHoras
{
    public static Result<ApuracaoBancoHoras> Calcular(DateOnly inicio, DateOnly fim, RegimeBancoHoras regime,
        SituacaoBancoHoras situacao, decimal salario, decimal divisor, decimal adicional,
        IReadOnlyList<LancamentoBancoHoras>? lancamentos)
    {
        if (!Enum.IsDefined(regime) || !Enum.IsDefined(situacao) || inicio > fim)
            return Erro.Validacao("Confira o regime e o período de apuração do banco de horas.");
        if (regime == RegimeBancoHoras.MesmoMes && (inicio.Year != fim.Year || inicio.Month != fim.Month)
            || regime == RegimeBancoHoras.AcordoIndividualEscrito && fim > inicio.AddMonths(6)
            || regime == RegimeBancoHoras.AcordoColetivo && fim > inicio.AddYears(1))
            return Erro.Validacao("O período excede o prazo do regime escolhido: mesmo mês, seis meses ou um ano.");
        if (salario < 0m || situacao != SituacaoBancoHoras.Acompanhamento && salario == 0m
            || salario > 1_000_000_000m || decimal.Round(salario, 2) != salario
            || divisor <= 0m || divisor > 744m || adicional < 50m || adicional > 1000m)
            return Erro.Validacao("Informe salário e divisor válidos e adicional entre 50% e 1.000%. Para quitar o saldo, o salário deve ser maior que zero.");
        if (lancamentos is null || lancamentos.Count == 0 || lancamentos.Count > 500)
            return Erro.Validacao("Informe de 1 a 500 lançamentos de crédito ou compensação.");

        var movimentos = new List<MovimentoBancoHoras>(lancamentos.Count);
        var saldo = 0;
        var creditos = 0;
        var compensacoes = 0;
        var creditoPorDia = new Dictionary<DateOnly, int>();
        foreach (var item in lancamentos.OrderBy(item => item.Data).ThenBy(item => item.Tipo))
        {
            if (!Enum.IsDefined(item.Tipo) || item.Data < inicio || item.Data > fim || item.Minutos <= 0 || item.Minutos > 600)
                return Erro.Validacao("Cada lançamento deve ter data no período e duração entre 1 minuto e 10 horas.");
            if (item.Tipo == TipoLancamentoBancoHoras.Credito)
            {
                creditoPorDia[item.Data] = creditoPorDia.GetValueOrDefault(item.Data) + item.Minutos;
                if (creditoPorDia[item.Data] > 120)
                    return Erro.Validacao($"Os créditos de {item.Data:dd/MM/yyyy} excedem duas horas extras no dia.");
                creditos += item.Minutos;
                saldo += item.Minutos;
            }
            else
            {
                compensacoes += item.Minutos;
                saldo -= item.Minutos;
            }
            movimentos.Add(new(item, saldo));
        }
        var valorHora = decimal.Round(salario / divisor, 4, MidpointRounding.AwayFromZero);
        var quitacao = decimal.Round(salario * (1m + adicional / 100m) * Math.Max(saldo, 0) / (divisor * 60m),
            2, MidpointRounding.AwayFromZero);
        return new ApuracaoBancoHoras(movimentos, creditos, compensacoes, saldo, valorHora, quitacao, situacao);
    }
}
