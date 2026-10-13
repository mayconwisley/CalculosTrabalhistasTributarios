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
        if (a.Dsr > 0m) verbas.Add(new($"DSR sobre {nome}", "", a.Dsr));
        foreach (var ano in a.DecimoTerceiro.Where(ano => ano.Valor > 0m))
            verbas.Add(new($"Reflexo no 13º de {ano.Ano}", Formato.Avos(ano.Meses), ano.Valor));
        if (a.Ferias > 0m)
        {
            verbas.Add(new("Reflexo nas férias", a.Verba.FeriasGozadas ? "Gozadas" : "Indenizadas", a.Ferias));
            verbas.Add(new("1/3 sobre o reflexo nas férias", "", a.TercoFerias));
        }
        if (a.AvisoPrevio > 0m) verbas.Add(new("Reflexo no aviso prévio indenizado", Formato.Dias(a.Verba.DiasAvisoPrevio), a.AvisoPrevio));
        return verbas;
    }

    /// <summary>O FGTS e a multa, depositados pelo empregador: informativos, fora do líquido.</summary>
    public static IReadOnlyList<VerbaDto> Informativos(ApuracaoReflexos a)
    {
        var verbas = new List<VerbaDto>();
        if (a.Verba.Incidencias.HasFlag(IncidenciasReflexo.Fgts)) verbas.Add(new("FGTS sobre a verba e os reflexos", "8%", a.Fgts));
        if (a.MultaFgts > 0m) verbas.Add(new("Multa de 40% do FGTS", "40%", a.MultaFgts));
        return verbas;
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
        if (a.Ferias > 0m)
            formulas.Add(new("Férias + 1/3", $"{Formato.Moeda(a.Meses.Sum(mes => mes.BaseReflexa))} ÷ 12 = {Formato.Moeda(a.Ferias)}; 1/3 = {Formato.Moeda(a.TercoFerias)}"));
        if (a.AvisoPrevio > 0m)
            formulas.Add(new("Aviso prévio", $"Média dos últimos {Meses(a.MesesAviso)} = {Formato.Moeda(a.MediaAviso)}; ÷ 30 x {Formato.Dias(a.Verba.DiasAvisoPrevio)} = {Formato.Moeda(a.AvisoPrevio)}"));
        if (a.Verba.Incidencias.HasFlag(IncidenciasReflexo.Fgts))
            formulas.Add(new("FGTS", $"{Formato.Moeda(a.BaseFgts)}{(a.Ferias > 0m && !a.Verba.FeriasGozadas ? " (sem as férias indenizadas)" : "")} x 8% = {Formato.Moeda(a.Fgts)}{(a.MultaFgts > 0m ? $"; multa de 40% = {Formato.Moeda(a.MultaFgts)}" : "")}"));
        return new GrupoMemoriaDto($"Reflexos de {a.Verba.Descricao}", $"Ao empregado: {Formato.Moeda(a.TotalAoEmpregado)}", formulas);
    }

    private static string Meses(int quantidade) => quantidade == 1 ? "1 mês" : $"{quantidade} meses";
}
