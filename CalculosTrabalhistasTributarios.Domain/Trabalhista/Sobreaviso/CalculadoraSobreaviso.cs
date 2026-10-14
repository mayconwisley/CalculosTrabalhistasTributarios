using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;

/// <summary>
/// Horas de sobreaviso e de prontidão (CLT, art. 244, §§ 2º e 3º, aplicados por analogia a outras categorias; Súmulas 229
/// e 428 do TST). O sobreaviso é pago à razão de 1/3 e a prontidão de 2/3 do salário-hora normal:
/// salário ÷ divisor × horas × 1/3 (ou 2/3), arredondado nos centavos por mês. A soma de cada mês segue para o motor de
/// reflexos com as premissas informadas.
/// </summary>
public static class CalculadoraSobreaviso
{
    public const decimal DivisorMaximo = 240m;
    private const decimal SalarioMaximo = 10_000_000m;

    public static Result<ApuracaoSobreaviso> Calcular(EntradaSobreaviso entrada)
    {
        if (entrada.Meses is null || entrada.Meses.Count is 0 or > MotorReflexos.MaximoDeMeses)
            return Erro.Validacao($"Informe de 1 a {MotorReflexos.MaximoDeMeses} competências de sobreaviso ou prontidão.");
        if (entrada.Divisor <= 0m || entrada.Divisor > DivisorMaximo)
            return Erro.Validacao($"O divisor do salário-hora deve ficar entre 1 e {DivisorMaximo:0}, como 220 para 44 horas semanais.");

        var meses = new List<SobreavisoDoMes>(entrada.Meses.Count);
        foreach (var mes in entrada.Meses.OrderBy(mes => mes.Competencia))
        {
            var competencia = $"{mes.Competencia:MM/yyyy}";
            if (mes.Salario <= 0m || mes.Salario > SalarioMaximo || decimal.Round(mes.Salario, 2) != mes.Salario)
                return Erro.Validacao($"Revise o salário de {competencia}: informe um valor positivo em reais e centavos.");
            if (mes.HorasSobreaviso < 0m || mes.HorasProntidao < 0m)
                return Erro.Validacao($"Revise as horas de {competencia}: elas não podem ser negativas.");
            // Sobreaviso e prontidão não se sobrepõem: juntos, cabem nas horas do mês.
            var horasDoMes = 24m * DateTime.DaysInMonth(mes.Competencia.Year, mes.Competencia.Month);
            if (mes.HorasSobreaviso + mes.HorasProntidao > horasDoMes)
                return Erro.Validacao($"As horas de sobreaviso e prontidão de {competencia} passam das {horasDoMes:0} horas do mês.");

            var sobreaviso = CalculadoraTributacao.Arredondar(mes.Salario / entrada.Divisor * mes.HorasSobreaviso / 3m);
            var prontidao = CalculadoraTributacao.Arredondar(mes.Salario / entrada.Divisor * mes.HorasProntidao * 2m / 3m);
            meses.Add(new SobreavisoDoMes(mes, sobreaviso, prontidao));
        }
        if (meses.All(mes => mes.Total == 0m))
            return Erro.Validacao("Informe as horas de sobreaviso ou de prontidão de pelo menos uma competência.");

        var verba = new VerbaReflexa(Descricao(meses), NaturezaVerbaReflexa.Outra, entrada.Habitual, entrada.Incidencias,
            meses.Select(mes => new MesVerbaReflexa(mes.Mes.Competencia, mes.Total, mes.Mes.Feriados)).ToArray(),
            entrada.DsrRepercuteNasOutras, entrada.DiasAvisoPrevio, entrada.FeriasGozadas, entrada.Admissao);
        var reflexos = MotorReflexos.Calcular(verba);
        return reflexos.Falhou ? reflexos.Erro : new ApuracaoSobreaviso(entrada, meses, reflexos.Valor);
    }

    private static string Descricao(IReadOnlyList<SobreavisoDoMes> meses) =>
        (meses.Any(mes => mes.ValorSobreaviso > 0m), meses.Any(mes => mes.ValorProntidao > 0m)) switch
        {
            (true, true) => "Sobreaviso e prontidão",
            (false, true) => "Horas de prontidão",
            _ => "Horas de sobreaviso"
        };
}
