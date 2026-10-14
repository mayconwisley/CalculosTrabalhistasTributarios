using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

/// <summary>
/// Reflexos de uma verba mês a mês, como uma diferença salarial, horas extras ou um adicional. Cada verba traz a sua
/// natureza, a habitualidade e as incidências: o motor aplica só o que foi pedido, sem presumir o direito.
/// <list type="bullet">
/// <item>DSR do mês: valor ÷ dias úteis × dias de descanso (Lei 605/1949, art. 7º).</item>
/// <item>13º: 1/12 da base reflexa de cada mês, somado por ano (Lei 4.090/1962).</item>
/// <item>Férias: 1/12 da base reflexa de cada mês do período aquisitivo, com o terço. Com a admissão, cada período é
/// classificado pela data de referência, o fim da última competência (CLT, arts. 130, 134, 146 e 147): no contrato ativo,
/// os completos são gozados e o período em curso ainda não é devido; na dispensa, o completo ainda no prazo de concessão é
/// indenizado como vencido, o incompleto como proporcional, e os anteriores se presumem gozados. Sem a admissão, todos os
/// meses formam um só período.</item>
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

        IReadOnlyList<FeriasReflexo> ferias = pedidas.HasFlag(IncidenciasReflexo.Ferias) ? PeriodosDeFerias(verba, meses) : [];
        var feriasGozadas = ferias.Where(periodo => periodo.Situacao == SituacaoFeriasReflexo.Gozadas).Sum(periodo => periodo.Valor + periodo.Terco);

        var ultimos = pedidas.HasFlag(IncidenciasReflexo.AvisoPrevio) ? meses.TakeLast(12).ToArray() : [];
        var mediaAviso = ultimos.Length > 0 ? Arredondar(ultimos.Sum(mes => mes.BaseReflexa) / ultimos.Length) : 0m;
        var aviso = ultimos.Length > 0 ? Arredondar(mediaAviso * verba.DiasAvisoPrevio / 30m) : 0m;

        var baseFgts = pedidas.HasFlag(IncidenciasReflexo.Fgts)
            ? meses.Sum(mes => mes.Valor + mes.Dsr) + decimoTerceiro.Sum(ano => ano.Valor) + feriasGozadas + aviso
            : 0m;
        var fgts = FormulasReflexos.Fgts(baseFgts);
        var multa = pedidas.HasFlag(IncidenciasReflexo.MultaFgts) ? FormulasReflexos.MultaFgts(fgts) : 0m;

        return new ApuracaoReflexos(verba, meses, decimoTerceiro, ferias, ultimos.Length, mediaAviso, aviso, baseFgts, fgts, multa);
    }

    /// <summary>
    /// Os períodos aquisitivos de 12 meses contados da admissão, com os meses da verba de cada um. A competência vai para o
    /// período que contém o dia 15 dela, onde fica a maior parte do mês.
    /// </summary>
    private static IReadOnlyList<FeriasReflexo> PeriodosDeFerias(VerbaReflexa verba, IReadOnlyList<ReflexoDoMes> meses)
    {
        var referencia = meses[^1].Competencia.AddMonths(1).AddDays(-1);
        if (verba.Admissao is not { } admissao)
        {
            var valor = Arredondar(meses.Sum(mes => mes.BaseReflexa) / 12m);
            return [new FeriasReflexo(meses[0].Competencia, referencia, Math.Min(12, meses.Count), meses.Sum(mes => mes.BaseReflexa), valor, FormulasReflexos.Terco(valor),
                verba.FeriasGozadas ? SituacaoFeriasReflexo.Gozadas : SituacaoFeriasReflexo.Indenizadas, Separado: false)];
        }

        return meses.GroupBy(mes => Periodo(admissao, new DateOnly(mes.Competencia.Year, mes.Competencia.Month, 15))).OrderBy(grupo => grupo.Key).Select(grupo =>
        {
            var inicio = admissao.AddYears(grupo.Key);
            var fim = admissao.AddYears(grupo.Key + 1).AddDays(-1);
            var completo = fim <= referencia;
            var situacao = (verba.FeriasGozadas, completo) switch
            {
                (true, true) => SituacaoFeriasReflexo.Gozadas,
                (true, false) => SituacaoFeriasReflexo.NaoDevidas,
                // Na dispensa, o período completo cujo prazo de concessão (12 meses após o fim) ainda corria não foi gozado.
                (false, true) => referencia < fim.AddYears(1) ? SituacaoFeriasReflexo.Indenizadas : SituacaoFeriasReflexo.Gozadas,
                _ => SituacaoFeriasReflexo.Indenizadas
            };
            var baseFerias = grupo.Sum(mes => mes.BaseReflexa);
            var valor = Arredondar(baseFerias / 12m);
            return new FeriasReflexo(inicio, fim, completo ? 12 : Avos(inicio, referencia), baseFerias, valor, FormulasReflexos.Terco(valor), situacao);
        }).ToArray();
    }

    private static int Periodo(DateOnly admissao, DateOnly data)
    {
        var anos = data.Year - admissao.Year;
        if (data < admissao.AddYears(anos))
            anos--;
        return Math.Max(0, anos);
    }

    /// <summary>Avos das férias proporcionais: 1/12 por mês de serviço, e a fração de 15 dias ou mais conta um mês (CLT, art. 146).</summary>
    private static int Avos(DateOnly inicio, DateOnly referencia)
    {
        var meses = 0;
        while (inicio.AddMonths(meses + 1).AddDays(-1) <= referencia)
            meses++;
        var dias = referencia.DayNumber - inicio.AddMonths(meses).DayNumber + 1;
        return Math.Min(12, meses + (dias >= 15 ? 1 : 0));
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
        if (verba.Admissao is { } admissao && verba.Meses.Min(mes => mes.Competencia) < new DateOnly(admissao.Year, admissao.Month, 1))
            return Erro.Validacao($"A verba {verba.Descricao} começa antes da admissão em {admissao.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}: revise a admissão ou as competências.");
        if (verba.Incidencias.HasFlag(IncidenciasReflexo.MultaFgts) && !verba.Incidencias.HasFlag(IncidenciasReflexo.Fgts))
            return Erro.Validacao("A multa de 40% incide sobre o FGTS: escolha também o reflexo no FGTS.");
        return null;
    }

    private static decimal Arredondar(decimal valor) => CalculadoraTributacao.Arredondar(valor);
}
