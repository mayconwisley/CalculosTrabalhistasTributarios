using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <summary>
/// Reflexos de uma verba mês a mês, como uma diferença salarial, horas extras ou um adicional. Cada verba traz a sua
/// natureza, a habitualidade e as incidências: o motor aplica só o que foi pedido, sem presumir o direito.
/// <list type="bullet">
/// <item>DSR do mês: valor ÷ dias úteis × dias de descanso (Lei 605/1949, art. 7º).</item>
/// <item>13º: 1/12 da base reflexa de cada mês, somado por ano (Lei 4.090/1962).</item>
/// <item>Férias: 1/12 da base reflexa de cada mês, com o terço; uma simulação sem separar os períodos aquisitivos.</item>
/// <item>Aviso prévio indenizado: média dos últimos 12 meses × dias ÷ 30 (CLT, art. 487, § 3º).</item>
/// <item>FGTS de 8% sobre a verba, o DSR e os reflexos, exceto as férias indenizadas; multa de 40% sobre ele.</item>
/// </list>
/// </summary>
public static class MotorReflexos
{
    public const int MaximoDeMeses = 120;

    /// <summary>
    /// Primeira competência em que o DSR das horas extras reflete nas demais parcelas. A tese vale para as horas extras
    /// trabalhadas desde 20/03/2023; a competência 03/2023, que mistura os dois regimes, segue a regra anterior.
    /// </summary>
    public static readonly DateOnly InicioRepercussaoDsrHorasExtras = new(2023, 4, 1);

    private const IncidenciasReflexo ExigemHabitualidade = IncidenciasReflexo.Dsr | IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.AvisoPrevio;

    public static Result<ApuracaoReflexos> Calcular(VerbaReflexa verba)
    {
        if (Validar(verba) is { } erro)
            return erro;
        var pedidas = verba.Incidencias;

        var meses = new List<ReflexoDoMes>(verba.Meses.Count);
        foreach (var mes in verba.Meses.OrderBy(mes => mes.Competencia))
        {
            var dias = RegrasTrabalhistas.DiasParaDsr(mes.Competencia, mes.Feriados);
            if (dias.Falhou)
                return dias.Erro;
            var (uteis, descanso) = dias.Valor;
            var dsr = pedidas.HasFlag(IncidenciasReflexo.Dsr) ? FormulasReflexos.Dsr(mes.Valor, descanso, uteis) : 0m;
            var repercute = dsr > 0m && (verba.Natureza == NaturezaVerbaReflexa.HorasExtras
                ? mes.Competencia >= InicioRepercussaoDsrHorasExtras
                : verba.DsrRepercuteNasOutras);
            meses.Add(new ReflexoDoMes(mes.Competencia, mes.Valor, uteis, descanso, dsr, repercute, mes.Valor + (repercute ? dsr : 0m)));
        }

        var decimoTerceiro = pedidas.HasFlag(IncidenciasReflexo.DecimoTerceiro)
            ? meses.GroupBy(mes => mes.Competencia.Year)
                .Select(ano => new DecimoTerceiroReflexo(ano.Key, ano.Count(), ano.Sum(mes => mes.BaseReflexa), Arredondar(ano.Sum(mes => mes.BaseReflexa) / 12m)))
                .ToArray()
            : [];

        var ferias = pedidas.HasFlag(IncidenciasReflexo.Ferias) ? Arredondar(meses.Sum(mes => mes.BaseReflexa) / 12m) : 0m;
        var terco = FormulasReflexos.Terco(ferias);

        var ultimos = pedidas.HasFlag(IncidenciasReflexo.AvisoPrevio) ? meses.TakeLast(12).ToArray() : [];
        var mediaAviso = ultimos.Length > 0 ? Arredondar(ultimos.Sum(mes => mes.BaseReflexa) / ultimos.Length) : 0m;
        var aviso = ultimos.Length > 0 ? Arredondar(mediaAviso * verba.DiasAvisoPrevio / 30m) : 0m;

        var baseFgts = pedidas.HasFlag(IncidenciasReflexo.Fgts)
            ? meses.Sum(mes => mes.Valor + mes.Dsr) + decimoTerceiro.Sum(ano => ano.Valor) + (verba.FeriasGozadas ? ferias + terco : 0m) + aviso
            : 0m;
        var fgts = FormulasReflexos.Fgts(baseFgts);
        var multa = pedidas.HasFlag(IncidenciasReflexo.MultaFgts) ? FormulasReflexos.MultaFgts(fgts) : 0m;

        return new ApuracaoReflexos(verba, meses, decimoTerceiro, ferias, terco, ultimos.Length, mediaAviso, aviso, baseFgts, fgts, multa);
    }

    private static Erro? Validar(VerbaReflexa verba)
    {
        if (verba.Meses is null || verba.Meses.Count is 0 or > MaximoDeMeses)
            return Erro.Validacao($"Informe de 1 a {MaximoDeMeses} meses da verba {verba.Descricao}.");
        if (!Enum.IsDefined(verba.Natureza) || (verba.Incidencias & ~(ExigemHabitualidade | IncidenciasReflexo.Fgts | IncidenciasReflexo.MultaFgts)) != 0)
            return Erro.Validacao($"Revise a natureza e os reflexos da verba {verba.Descricao}.");
        var competencias = new HashSet<DateOnly>();
        foreach (var mes in verba.Meses)
        {
            if (mes.Competencia.Day != 1 || !competencias.Add(mes.Competencia))
                return Erro.Validacao($"Cada competência da verba {verba.Descricao} deve aparecer uma vez, no formato MM/AAAA.");
            if (mes.Valor < 0m || mes.Valor > 1_000_000_000m || decimal.Round(mes.Valor, 2) != mes.Valor || mes.Feriados < 0)
                return Erro.Validacao($"Revise o valor e os feriados de {mes.Competencia:MM/yyyy} da verba {verba.Descricao}: use valores não negativos em reais e centavos.");
        }
        // Só a verba habitual integra a remuneração para o DSR, o 13º, as férias e o aviso (Súmulas 45 e 172 do TST;
        // CLT, art. 487, § 3º); a eventual tem apenas o FGTS do mês em que é paga.
        if (!verba.Habitual && (verba.Incidencias & ExigemHabitualidade) != 0)
            return Erro.Validacao($"Os reflexos em DSR, 13º, férias e aviso prévio exigem que a verba {verba.Descricao} seja habitual.");
        if (verba.Incidencias.HasFlag(IncidenciasReflexo.AvisoPrevio) ? verba.DiasAvisoPrevio is < 30 or > 90 : verba.DiasAvisoPrevio != 0)
            return Erro.Validacao("O aviso prévio indenizado tem de 30 a 90 dias (Lei 12.506/2011); informe os dias só com o reflexo no aviso.");
        if (verba.Incidencias.HasFlag(IncidenciasReflexo.MultaFgts) && !verba.Incidencias.HasFlag(IncidenciasReflexo.Fgts))
            return Erro.Validacao("A multa de 40% incide sobre o FGTS: escolha também o reflexo no FGTS.");
        return null;
    }

    private static decimal Arredondar(decimal valor) => CalculadoraTributacao.Arredondar(valor);
}
