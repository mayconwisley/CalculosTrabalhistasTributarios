using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;

/// <summary>
/// Gorjetas (CLT, art. 457, §§ 3º a 11, incluídos pela Lei 13.419/2017). A gorjeta, cobrada na nota ou dada
/// espontaneamente, não é receita da empresa e integra a remuneração do empregado. Com previsão em convenção ou acordo
/// coletivo, a empresa pode reter até 20% (Simples Nacional) ou até 33% (demais regimes) para custear os encargos; o
/// restante é repassado ao empregado (§ 6º). As gorjetas repassadas refletem no 13º, nas férias e no FGTS, mas não são
/// base do aviso prévio, do adicional noturno, das horas extras nem do repouso semanal (Súmula 354 do TST): o motor
/// recusa esses reflexos.
/// </summary>
public static class CalculadoraGorjetas
{
    private const decimal ValorMaximo = 10_000_000m;
    private const IncidenciasReflexo ExcluidasPelaSumula354 = IncidenciasReflexo.Dsr | IncidenciasReflexo.AvisoPrevio;

    public static decimal LimiteRetencao(bool regimeDiferenciado) => regimeDiferenciado ? 20m : 33m;

    public static Result<ApuracaoGorjetas> Calcular(EntradaGorjetas entrada)
    {
        if (entrada.Meses is null || entrada.Meses.Count is 0 or > MotorReflexos.MaximoDeMeses)
            return Erro.Validacao($"Informe de 1 a {MotorReflexos.MaximoDeMeses} competências de gorjetas.");
        var limite = LimiteRetencao(entrada.RegimeDiferenciado);
        if (entrada.PercentualRetencao < 0m || entrada.PercentualRetencao > limite)
            return Erro.Validacao($"A retenção da empresa vai de 0% a {limite:0}% {(entrada.RegimeDiferenciado ? "no Simples Nacional" : "fora do Simples Nacional")} (CLT, art. 457, § 6º).");
        if ((entrada.Incidencias & ExcluidasPelaSumula354) != 0)
            return Erro.Validacao("As gorjetas não são base do repouso semanal nem do aviso prévio (Súmula 354 do TST).");

        var meses = new List<GorjetaDoMes>(entrada.Meses.Count);
        foreach (var mes in entrada.Meses.OrderBy(mes => mes.Competencia))
        {
            if (mes.Valor < 0m || mes.Valor > ValorMaximo || decimal.Round(mes.Valor, 2) != mes.Valor)
                return Erro.Validacao($"Revise as gorjetas de {mes.Competencia:MM/yyyy}: informe um valor em reais e centavos, ou 0,00.");
            var retencao = CalculadoraTributacao.Arredondar(mes.Valor * entrada.PercentualRetencao / 100m);
            meses.Add(new GorjetaDoMes(mes, retencao, mes.Valor - retencao));
        }
        if (meses.All(mes => mes.Repasse == 0m))
            return Erro.Validacao("Informe as gorjetas de pelo menos uma competência.");

        var verba = new VerbaReflexa("Gorjetas", NaturezaVerbaReflexa.Outra, true, entrada.Incidencias,
            meses.Select(mes => new MesVerbaReflexa(mes.Mes.Competencia, mes.Repasse)).ToArray(), FeriasGozadas: entrada.FeriasGozadas, Admissao: entrada.Admissao);
        var reflexos = MotorReflexos.Calcular(verba);
        if (reflexos.Falhou)
            return reflexos.Erro;

        // Média para a incorporação quando a cobrança, feita por mais de 12 meses, é suprimida (art. 457, § 9º).
        var ultimos = meses.TakeLast(12).ToArray();
        var media = CalculadoraTributacao.Arredondar(ultimos.Sum(mes => mes.Repasse) / ultimos.Length);
        return new ApuracaoGorjetas(entrada, meses, reflexos.Valor, ultimos.Length, media);
    }
}
