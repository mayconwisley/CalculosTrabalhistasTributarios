using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

/// <summary>
/// Demonstrativo da indenização do período de estabilidade a partir do resultado já apurado: as mesmas verbas, o mesmo
/// total e a mesma memória da janela e do PDF do desktop, sem recalcular a regra.
/// </summary>
public static class DemonstrativoEstabilidade
{
    public static DemonstrativoDto Montar(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada)
    {
        var periodo = Periodo(simulacao);
        var observacoes = new List<string>
        {
            "O total soma a indenização dos salários do período, o 13º salário, as férias com 1/3, o FGTS de 8% com a multa de 40% e os complementos informados."
        };
        var proventos = new List<VerbaDto>
        {
            new("Indenização", periodo, simulacao.Indenizacao),
            new("13º salário", Formato.Avos(simulacao.AvosDecimoTerceiro), simulacao.DecimoTerceiro),
            new("Férias", Formato.Avos(simulacao.AvosFerias), simulacao.Ferias),
            new("1/3 de férias", "", simulacao.TercoFerias),
            new("FGTS 8%", "", simulacao.FgtsOitoPorCento),
            new("FGTS 40%", "", simulacao.MultaFgtsQuarentaPorCento),
            new("Complementos", "", simulacao.Complementos)
        };
        // O total vem arredondado da apuração; um complemento com mais de duas casas faz a soma das linhas diferir dele.
        var soma = proventos.Sum(verba => verba.Valor);
        if (soma != simulacao.Total)
            observacoes.Add($"A soma das linhas, {Formato.MoedaExata(soma)}, difere do total estimado de {Formato.Moeda(simulacao.Total)} pelo arredondamento do total em centavos.");

        return new DemonstrativoDto(
            "Estabilidade",
            $"Demissão em {Formato.Data(entrada.Demissao)} • fim da estabilidade em {Formato.Data(entrada.FimEstabilidade)}",
            [
                new("Indenização", Formato.Moeda(simulacao.Indenizacao), periodo),
                new("13º salário", Formato.Moeda(simulacao.DecimoTerceiro), $"{simulacao.AvosDecimoTerceiro} avo(s)"),
                new("Férias + 1/3", Formato.Moeda(simulacao.Ferias + simulacao.TercoFerias), $"{simulacao.AvosFerias} avo(s); férias: {Formato.Moeda(simulacao.Ferias)}"),
                new("FGTS + multa", Formato.Moeda(simulacao.FgtsOitoPorCento + simulacao.MultaFgtsQuarentaPorCento), "8% + multa de 40%"),
                new("Complementos", Formato.Moeda(simulacao.Complementos), "Ajuste adicional informado"),
                new("Total estimado", Formato.Moeda(simulacao.Total), "Soma das verbas calculadas")
            ],
            proventos,
            [],
            [],
            [DadosConsiderados(simulacao, entrada), Memoria(simulacao, entrada, periodo)],
            observacoes,
            RotuloProventos: "Verbas da indenização",
            RotuloResultado: "Total estimado");
    }

    private static GrupoMemoriaDto DadosConsiderados(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada) => new(
        "Dados considerados no cálculo",
        $"{Formato.Dias(simulacao.DiasEstabilidade)} de estabilidade",
        [
            new("Demissão", Formato.Data(entrada.Demissao)),
            new("Fim da estabilidade", Formato.Data(entrada.FimEstabilidade)),
            new("Média para cálculo", Formato.Moeda(entrada.MediaRemuneratoria)),
            new("Dias-base", entrada.DiasBase.ToString(Formato.Cultura)),
            new("Dias de estabilidade", simulacao.DiasEstabilidade.ToString(Formato.Cultura)),
            new("Meses e dias", $"{simulacao.Meses} mês(es) e {simulacao.DiasAlemDosMeses} dia(s)"),
            new("Avos de 13º", simulacao.AvosDecimoTerceiro.ToString(Formato.Cultura)),
            new("Avos de férias", simulacao.AvosFerias.ToString(Formato.Cultura))
        ]);

    private static GrupoMemoriaDto Memoria(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada, string periodo)
    {
        var media = Formato.Moeda(entrada.MediaRemuneratoria);
        return new GrupoMemoriaDto(
            "Memória de cálculo",
            $"Total estimado: {Formato.Moeda(simulacao.Total)}",
            [
                new("Período", $"{simulacao.DiasEstabilidade.ToString("N0", Formato.Cultura)} dias após a demissão: {periodo}"),
                new("Indenização", FormulaIndenizacao(simulacao, entrada)),
                new("13º salário", $"{media} ÷ 12 × {simulacao.AvosDecimoTerceiro} avo(s) = {Formato.Moeda(simulacao.DecimoTerceiro)}: meses do ano com 15 dias ou mais de contrato, além dos pagos na rescisão"),
                new("Férias proporcionais", $"{media} ÷ 12 × {simulacao.AvosFerias} avo(s) = {Formato.Moeda(simulacao.Ferias)}"),
                new("Adicional de férias", $"{Formato.Moeda(simulacao.Ferias)} ÷ 3 = {Formato.Moeda(simulacao.TercoFerias)}"),
                new("FGTS", $"({Formato.Moeda(simulacao.Indenizacao)} + {Formato.Moeda(simulacao.DecimoTerceiro)}) × 8% = {Formato.Moeda(simulacao.FgtsOitoPorCento)}"),
                new("Multa rescisória do FGTS", $"{Formato.Moeda(simulacao.FgtsOitoPorCento)} × 40% = {Formato.Moeda(simulacao.MultaFgtsQuarentaPorCento)}"),
                new("Total estimado", $"Verbas calculadas + complementos de {Formato.Moeda(simulacao.Complementos)} = {Formato.Moeda(simulacao.Total)}")
            ]);
    }

    private static string Periodo(SimulacaoEstabilidadeDto simulacao)
    {
        var meses = simulacao.Meses == 1 ? "1 mês" : $"{simulacao.Meses} meses";
        var dias = Formato.Dias(simulacao.DiasAlemDosMeses);
        return simulacao.Meses == 0 ? dias : simulacao.DiasAlemDosMeses == 0 ? meses : $"{meses} e {dias}";
    }

    // Os meses cheios valem a média; os dias que sobram, a média diária pelos dias-base.
    private static string FormulaIndenizacao(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada)
    {
        var media = Formato.Moeda(entrada.MediaRemuneratoria);
        var termos = new List<string>();
        if (simulacao.Meses > 0)
            termos.Add($"{media} × {simulacao.Meses} {(simulacao.Meses == 1 ? "mês" : "meses")}");
        if (simulacao.DiasAlemDosMeses > 0)
            termos.Add($"{media} ÷ {entrada.DiasBase} × {Formato.Dias(simulacao.DiasAlemDosMeses)}");
        return $"{string.Join(" + ", termos)} = {Formato.Moeda(simulacao.Indenizacao)}";
    }
}
