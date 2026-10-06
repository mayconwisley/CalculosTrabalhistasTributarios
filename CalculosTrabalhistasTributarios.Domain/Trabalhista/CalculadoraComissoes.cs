using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>Apura o repouso remunerado das comissões sem misturá-lo ao DSR das horas extras.</summary>
public static class CalculadoraComissoes
{
    public static decimal ComplementoGarantiaMinima(decimal remuneracao, decimal piso) =>
        Math.Max(0m, piso - remuneracao);

    public static Result<ComissoesApuradas> Calcular(ComissoesInformadas entrada)
    {
        if (entrada.Valor < 0m)
            return Erro.Validacao("O valor das comissões não pode ser negativo.");
        if (entrada.Feriados < 0 || entrada.DescansosPerdidos < 0)
            return Erro.Validacao("Os feriados e os descansos perdidos não podem ser negativos.");
        if (entrada.DiasUteis.HasValue != entrada.DiasDescanso.HasValue)
            return Erro.Validacao("Informe juntos os dias úteis e os dias de descanso, ou use a contagem automática.");

        int diasUteis, diasDescanso;
        if (entrada.DiasUteis is { } uteis && entrada.DiasDescanso is { } repousos)
        {
            var diasNoMes = DateTime.DaysInMonth(entrada.Competencia.Year, entrada.Competencia.Month);
            if (uteis <= 0 || repousos < 0 || (long)uteis + repousos > diasNoMes)
                return Erro.Validacao($"Os dias úteis devem ser positivos e, somados aos descansos, não podem passar dos {diasNoMes} dias da competência.");
            diasUteis = uteis;
            diasDescanso = repousos;
        }
        else
        {
            var dias = RegrasTrabalhistas.DiasParaDsr(entrada.Competencia, entrada.Feriados);
            if (dias.Falhou)
                return dias.Erro;
            (diasUteis, diasDescanso) = dias.Valor;
        }

        if (entrada.DescansosPerdidos > diasDescanso)
            return Erro.Validacao($"Os descansos perdidos não podem passar dos {diasDescanso} repousos do período.");

        var descansosPagos = diasDescanso - entrada.DescansosPerdidos;
        decimal comissoes, dsr;
        if (entrada.IncluiDsr)
        {
            // O valor informado já contém as duas parcelas. Arredondar a comissão e calcular o DSR como diferença
            // preserva o total informado, inclusive quando a divisão produz frações de centavo.
            comissoes = CalculadoraTributacao.Arredondar(entrada.Valor * diasUteis / (diasUteis + descansosPagos));
            dsr = entrada.Valor - comissoes;
        }
        else
        {
            comissoes = entrada.Valor;
            dsr = CalculadoraTributacao.Arredondar(comissoes * descansosPagos / diasUteis);
        }

        return new ComissoesApuradas(entrada.Valor, comissoes, dsr, diasUteis, diasDescanso,
            entrada.DescansosPerdidos, entrada.IncluiDsr, entrada.DiasUteis.HasValue);
    }
}
