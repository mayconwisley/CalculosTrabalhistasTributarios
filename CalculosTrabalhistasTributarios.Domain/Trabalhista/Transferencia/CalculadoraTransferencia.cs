using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Transferencia;

/// <summary>
/// Adicional de transferência provisória (CLT, art. 469, § 3º): pagamento suplementar de no mínimo 25% dos salários que
/// o empregado recebia na localidade de origem, enquanto durar a transferência. A transferência definitiva não gera o
/// adicional; o cargo de confiança ou a cláusula de transferência no contrato não o afastam (OJ 113 da SBDI-1 do TST).
/// Por mês: salário × percentual × dias ÷ 30, arredondado nos centavos. O adicional tem natureza salarial e segue para o
/// motor de reflexos; como incide sobre o salário mensal, que já remunera o repouso, não tem DSR.
/// </summary>
public static class CalculadoraTransferencia
{
    public const decimal PercentualMinimo = 25m;
    private const decimal SalarioMaximo = 10_000_000m;

    public static Result<ApuracaoTransferencia> Calcular(EntradaTransferencia entrada)
    {
        if (entrada.Meses is null || entrada.Meses.Count is 0 or > MotorReflexos.MaximoDeMeses)
            return Erro.Validacao($"Informe de 1 a {MotorReflexos.MaximoDeMeses} competências da transferência.");
        if (entrada.Percentual < PercentualMinimo || entrada.Percentual > 100m)
            return Erro.Validacao($"O adicional de transferência é de no mínimo {PercentualMinimo:0}% (CLT, art. 469, § 3º); informe de 25% a 100%.");
        if (entrada.Despesas < 0m || entrada.Despesas > SalarioMaximo || decimal.Round(entrada.Despesas, 2) != entrada.Despesas)
            return Erro.Validacao("Revise as despesas da transferência: informe um valor em reais e centavos, ou 0,00.");
        if (entrada.Incidencias.HasFlag(IncidenciasReflexo.Dsr))
            return Erro.Validacao("O adicional incide sobre o salário mensal, que já remunera o repouso: não há DSR sobre ele.");

        var meses = new List<AdicionalTransferenciaDoMes>(entrada.Meses.Count);
        foreach (var mes in entrada.Meses.OrderBy(mes => mes.Competencia))
        {
            var competencia = $"{mes.Competencia:MM/yyyy}";
            if (mes.Salario <= 0m || mes.Salario > SalarioMaximo || decimal.Round(mes.Salario, 2) != mes.Salario)
                return Erro.Validacao($"Revise o salário de {competencia}: informe um valor positivo em reais e centavos.");
            if (mes.Dias is < 0 or > 30)
                return Erro.Validacao($"Revise os dias transferido em {competencia}: de 0 a 30, sendo 30 o mês inteiro.");
            meses.Add(new AdicionalTransferenciaDoMes(mes, CalculadoraTributacao.Arredondar(mes.Salario * entrada.Percentual / 100m * mes.Dias / 30m)));
        }
        if (meses.All(mes => mes.Valor == 0m))
            return Erro.Validacao("Informe os dias transferido de pelo menos uma competência.");

        var verba = new VerbaReflexa("Adicional de transferência", NaturezaVerbaReflexa.Outra, true, entrada.Incidencias,
            meses.Select(mes => new MesVerbaReflexa(mes.Mes.Competencia, mes.Valor)).ToArray(),
            DiasAvisoPrevio: entrada.DiasAvisoPrevio, FeriasGozadas: entrada.FeriasGozadas, Admissao: entrada.Admissao);
        var reflexos = MotorReflexos.Calcular(verba);
        return reflexos.Falhou ? reflexos.Erro : new ApuracaoTransferencia(entrada, meses, reflexos.Valor);
    }
}
