namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <summary>Reflexos de uma verba, com os valores intermediários para a memória de cálculo.</summary>
/// <param name="MediaAviso">Média mensal das bases reflexas dos últimos 12 meses, base do aviso prévio.</param>
/// <param name="PeriodosFerias">O reflexo nas férias de cada período aquisitivo, devidas ou não.</param>
/// <param name="BaseFgts">Verba, DSR, 13º, férias gozadas com o terço e aviso prévio: o que recebe o FGTS.</param>
public sealed record ApuracaoReflexos(
    VerbaReflexa Verba,
    IReadOnlyList<ReflexoDoMes> Meses,
    IReadOnlyList<DecimoTerceiroReflexo> DecimoTerceiro,
    IReadOnlyList<FeriasReflexo> PeriodosFerias,
    int MesesAviso,
    decimal MediaAviso,
    decimal AvisoPrevio,
    decimal BaseFgts,
    decimal Fgts,
    decimal MultaFgts)
{
    public decimal Principal => Meses.Sum(mes => mes.Valor);
    public decimal Dsr => Meses.Sum(mes => mes.Dsr);
    public decimal TotalDecimoTerceiro => DecimoTerceiro.Sum(ano => ano.Valor);

    /// <summary>O reflexo nas férias devidas, gozadas ou indenizadas, sem o período em curso de um contrato ativo.</summary>
    public decimal Ferias => PeriodosFerias.Where(periodo => periodo.Devidas).Sum(periodo => periodo.Valor);
    public decimal TercoFerias => PeriodosFerias.Where(periodo => periodo.Devidas).Sum(periodo => periodo.Terco);

    /// <summary>O que o empregado recebe: a verba e os reflexos, sem o FGTS e a multa, que são depositados.</summary>
    public decimal TotalAoEmpregado => Principal + Dsr + TotalDecimoTerceiro + Ferias + TercoFerias + AvisoPrevio;
}
