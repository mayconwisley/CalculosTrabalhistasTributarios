using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>Linhas do demonstrativo e memória de cálculo dos reflexos apurados pelo motor de reflexos.</summary>
public static class MemoriaReflexos
{
    /// <summary>A verba e os reflexos pagos ao empregado, como proventos; os vazios ficam de fora.</summary>
    public static IReadOnlyList<VerbaDto> Proventos(ApuracaoReflexos a)
    {
        var nome = a.Verba.Descricao;
        var verbas = new List<VerbaDto> { new(nome, Meses(a.Meses.Count), a.Principal) };
        if (a.Dsr > 0m) verbas.Add(new($"DSR sobre {NoMeioDaFrase(nome)}", "", a.Dsr));
        foreach (var ano in a.DecimoTerceiro.Where(ano => ano.Valor > 0m))
            verbas.Add(new($"Reflexo no 13º de {ano.Ano}", Formato.Avos(ano.Meses), ano.Valor));
        foreach (var periodo in a.PeriodosFerias.Where(periodo => periodo.Devidas && periodo.Valor > 0m))
        {
            var reflexo = periodo.Separado ? $"Reflexo nas férias {Situacao(periodo)}" : "Reflexo nas férias";
            verbas.Add(new(reflexo, periodo.Separado ? $"Desde {Formato.Data(periodo.Inicio)}" : periodo.Situacao == SituacaoFeriasReflexo.Gozadas ? "Gozadas" : "Indenizadas", periodo.Valor));
            verbas.Add(new($"1/3 sobre o {NoMeioDaFrase(reflexo)}", "", periodo.Terco));
        }
        if (a.AvisoPrevio > 0m) verbas.Add(new("Reflexo no aviso prévio indenizado", Formato.Dias(a.Verba.DiasAvisoPrevio), a.AvisoPrevio));
        return verbas;
    }

    /// <summary>O FGTS e a multa, depositados pelo empregador: informativos, fora do líquido.</summary>
    public static IReadOnlyList<VerbaDto> Informativos(ApuracaoReflexos a)
    {
        var verbas = new List<VerbaDto>();
        foreach (var periodo in a.PeriodosFerias.Where(periodo => !periodo.Devidas && periodo.Valor > 0m))
            verbas.Add(new($"Férias + 1/3 do período em curso ({Formato.Avos(periodo.Avos)}, ainda não devidas)", $"Desde {Formato.Data(periodo.Inicio)}", periodo.Valor + periodo.Terco));
        if (a.Verba.Incidencias.HasFlag(IncidenciasReflexo.Fgts)) verbas.Add(new("FGTS sobre a verba e os reflexos", "8%", a.Fgts));
        if (a.MultaFgts > 0m) verbas.Add(new("Multa de 40% do FGTS", "40%", a.MultaFgts));
        return verbas;
    }

    /// <summary>Como as férias foram separadas e classificadas; nula quando a verba não reflete nas férias.</summary>
    public static string? ObservacaoFerias(ApuracaoReflexos a)
    {
        if (a.PeriodosFerias.Count == 0)
            return null;
        if (!a.PeriodosFerias[0].Separado)
            return "Sem a data de admissão, o reflexo nas férias soma todos os meses informados num só período, " +
                (a.Verba.FeriasGozadas ? "como férias gozadas, inclusive os meses de um período aquisitivo ainda em curso, cujas férias não seriam devidas." : "como férias indenizadas, sem FGTS, inclusive os de períodos cujas férias foram gozadas.") +
                " Informe a admissão para separar os períodos aquisitivos.";
        return "Férias por período aquisitivo de 12 meses contados da admissão (CLT, arts. 130 e 134), até o fim da última competência informada: " +
            (a.Verba.FeriasGozadas
                ? "no contrato ativo, as férias dos períodos completos são gozadas, com FGTS, e as do período em curso ainda não são devidas."
                : "na dispensa, o período completo ainda no prazo de concessão é indenizado como férias vencidas, o incompleto como proporcionais (art. 146), ambos sem FGTS (OJ 195 da SBDI-1 do TST), e os anteriores se presumem gozados, com FGTS.") +
            " Férias pagas em dobro fora do prazo (art. 137) e a redução dos dias por faltas (art. 130) não são simuladas.";
    }

    public static GrupoMemoriaDto Grupo(ApuracaoReflexos a)
    {
        var formulas = new List<FormulaDto> { new("Verba", $"{a.Verba.Descricao}: {Formato.Moeda(a.Principal)} em {Meses(a.Meses.Count)}{(a.Verba.Habitual ? ", habitual" : ", eventual")}") };
        if (a.Dsr > 0m)
        {
            formulas.AddRange(a.Meses.Where(mes => mes.Dsr > 0m).Select(mes => new FormulaDto($"DSR de {Formato.Competencia(mes.Competencia)}",
                $"{Formato.Moeda(mes.Valor)} ÷ {mes.DiasUteis} dias úteis x {mes.DiasDescanso} de descanso = {Formato.Moeda(mes.Dsr)}{(mes.DsrRepercute ? "; entra na base dos reflexos" : "")}")));
            if (a.Verba.Natureza == NaturezaVerbaReflexa.HorasExtras)
                formulas.Add(new("DSR nos demais reflexos", $"Só nas competências desde {Formato.Competencia(MotorReflexos.InicioRepercussaoDsrHorasExtras)}: o DSR das horas extras trabalhadas desde 20/03/2023 reflete no 13º, nas férias, no aviso e no FGTS (TST, Tema Repetitivo 9, que alterou a OJ 394)"));
        }
        foreach (var ano in a.DecimoTerceiro)
            formulas.Add(new($"13º de {ano.Ano}", $"{Formato.Moeda(ano.Base)} (base de {Meses(ano.Meses)}) ÷ 12 = {Formato.Moeda(ano.Valor)}"));
        foreach (var periodo in a.PeriodosFerias)
            formulas.Add(periodo.Separado
                ? new($"Férias de {Periodo(periodo)}", $"{Formato.Moeda(periodo.Base)} ÷ 12 = {Formato.Moeda(periodo.Valor)}; 1/3 = {Formato.Moeda(periodo.Terco)}; {Explicacao(periodo)}")
                : new("Férias + 1/3", $"{Formato.Moeda(periodo.Base)} ÷ 12 = {Formato.Moeda(periodo.Valor)}; 1/3 = {Formato.Moeda(periodo.Terco)}; sem a admissão, os meses não são separados por período aquisitivo"));
        if (a.AvisoPrevio > 0m)
            formulas.Add(new("Aviso prévio", $"Média dos últimos {Meses(a.MesesAviso)} = {Formato.Moeda(a.MediaAviso)}; ÷ 30 x {Formato.Dias(a.Verba.DiasAvisoPrevio)} = {Formato.Moeda(a.AvisoPrevio)}"));
        if (a.Verba.Incidencias.HasFlag(IncidenciasReflexo.Fgts))
            formulas.Add(new("FGTS", $"{Formato.Moeda(a.BaseFgts)}{(a.PeriodosFerias.Any(periodo => periodo.Situacao == SituacaoFeriasReflexo.Indenizadas && periodo.Valor > 0m) ? " (sem as férias indenizadas)" : "")} x 8% = {Formato.Moeda(a.Fgts)}{(a.MultaFgts > 0m ? $"; multa de 40% = {Formato.Moeda(a.MultaFgts)}" : "")}"));
        return new GrupoMemoriaDto($"Reflexos de {NoMeioDaFrase(a.Verba.Descricao)}", $"Ao empregado: {Formato.Moeda(a.TotalAoEmpregado)}", formulas);
    }

    /// <summary>A descrição da verba no meio de uma frase, como "DSR sobre horas extras".</summary>
    private static string NoMeioDaFrase(string descricao) => descricao.Length == 0 ? descricao : char.ToLower(descricao[0], Formato.Cultura) + descricao[1..];

    private static string Periodo(FeriasReflexo periodo) => $"{Formato.Data(periodo.Inicio)} a {Formato.Data(periodo.Fim)}";

    private static string Situacao(FeriasReflexo periodo) => periodo.Situacao switch
    {
        SituacaoFeriasReflexo.Gozadas => "gozadas",
        SituacaoFeriasReflexo.NaoDevidas => "em aquisição",
        _ => periodo.Completo ? "vencidas" : "proporcionais"
    };

    private static string Explicacao(FeriasReflexo periodo) => periodo.Situacao switch
    {
        SituacaoFeriasReflexo.Gozadas => "período completo, férias gozadas, com FGTS",
        SituacaoFeriasReflexo.NaoDevidas => $"período em curso ({Formato.Avos(periodo.Avos)}): férias ainda não devidas",
        _ when periodo.Completo => "período completo ainda no prazo de concessão: vencidas, indenizadas sem FGTS",
        _ => $"período incompleto ({Formato.Avos(periodo.Avos)}): proporcionais, indenizadas sem FGTS"
    };

    private static string Meses(int quantidade) => quantidade == 1 ? "1 mês" : $"{quantidade} meses";
}
