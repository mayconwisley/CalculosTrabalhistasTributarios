using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Domain.Estabilidade;

/// <summary>
/// Regras puras para a apuração de indenização do período de estabilidade.
/// </summary>
public static class CalculadoraEstabilidade
{
    public static Result<ResultadoEstabilidade> Calcular(decimal mediaRemuneratoria, int diasBase, DateOnly demissao, DateOnly fimEstabilidade, decimal complementos = 0m)
    {
        if (mediaRemuneratoria < 0m)
            return Erro.Validacao("A média remuneratória não pode ser negativa.");
        if (diasBase <= 0)
            return Erro.Validacao("Os dias-base devem ser maiores que zero.");

        var diasEstabilidade = fimEstabilidade.DayNumber - demissao.DayNumber;
        if (diasEstabilidade <= 0)
            return Erro.Validacao("O fim da estabilidade deve ser posterior à data de demissão.");

        // Os salários do período (Súmula 396 do TST): os meses cheios pela média e os dias que sobram pela média diária.
        var inicio = demissao.AddDays(1);
        var meses = 0;
        while (inicio.AddMonths(meses + 1).AddDays(-1) <= fimEstabilidade)
            meses++;
        var diasAlemDosMeses = fimEstabilidade.DayNumber - inicio.AddMonths(meses).DayNumber + 1;
        var indenizacao = Arredondar(mediaRemuneratoria * meses + mediaRemuneratoria * diasAlemDosMeses / diasBase);

        // 13º: os meses do ano civil com 15 dias ou mais de contrato, somando os dias já trabalhados no mês da demissão
        // e descontando os avos já pagos na rescisão. Férias: os meses do período, com a fração de 15 dias ou mais.
        var inicioMesDemissao = new DateOnly(demissao.Year, demissao.Month, 1);
        var avosDecimoTerceiro = Enumerable.Range(demissao.Year, fimEstabilidade.Year - demissao.Year + 1)
            .Sum(ano => RegrasTrabalhistas.AvosDecimoTerceiro(inicioMesDemissao, fimEstabilidade, ano) - RegrasTrabalhistas.AvosDecimoTerceiro(inicioMesDemissao, demissao, ano));
        var avosFerias = RegrasTrabalhistas.AvosFerias(inicio, fimEstabilidade);

        var decimoTerceiro = Arredondar(mediaRemuneratoria * avosDecimoTerceiro / 12m);
        var ferias = Arredondar(mediaRemuneratoria * avosFerias / 12m);
        var tercoFerias = Arredondar(ferias / 3m);
        var fgtsOitoPorCento = Arredondar((indenizacao + decimoTerceiro) * .08m);
        var multaFgtsQuarentaPorCento = Arredondar(fgtsOitoPorCento * .40m);
        var total = Arredondar(indenizacao + decimoTerceiro + ferias + tercoFerias + fgtsOitoPorCento + multaFgtsQuarentaPorCento + complementos);

        return new ResultadoEstabilidade(diasEstabilidade, meses, diasAlemDosMeses, avosDecimoTerceiro, avosFerias, indenizacao, decimoTerceiro, ferias, tercoFerias, fgtsOitoPorCento, multaFgtsQuarentaPorCento, complementos, total);
    }

    private static decimal Arredondar(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
}
