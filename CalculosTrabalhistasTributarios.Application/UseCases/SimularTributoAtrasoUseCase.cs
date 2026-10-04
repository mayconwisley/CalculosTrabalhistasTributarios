using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Atualizacao;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Valor de uma guia paga depois do vencimento: principal, multa de mora e juros pela Selic.</summary>
public sealed class SimularTributoAtrasoUseCase(IIndicesEconomicos indices) : ISimularDemonstrativoUseCase<SimularTributoAtrasoRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularTributoAtrasoRequest r, CancellationToken cancellationToken)
    {
        if (r.Pagamento < r.Vencimento)
            return Erro.Validacao("A data do pagamento não pode ser anterior ao vencimento.");

        var selic = await indices.ObterAsync(IndiceEconomico.Selic, cancellationToken);
        var calculo = CalculadoraTributoEmAtraso.Calcular(r.Principal, r.Vencimento, r.Pagamento, selic);
        if (calculo.Falhou)
            return calculo.Erro;
        var a = calculo.Valor;
        var guia = NomeGuia(r.Guia);

        var formulas = new List<FormulaDto>
        {
            new("Dias de atraso", a.DiasDeAtraso == 0
                ? "Pago até o vencimento: sem multa nem juros."
                : $"De {Formato.Data(r.Vencimento.AddDays(1))} a {Formato.Data(r.Pagamento)}: {Formato.Dias(a.DiasDeAtraso)}"),
            new("Multa de mora", a.DiasDeAtraso == 0
                ? Formato.Moeda(0m)
                : $"{Formato.Dias(a.DiasDeAtraso)} x 0,33% = {Formato.Percentual(a.DiasDeAtraso * CalculadoraTributoEmAtraso.MultaPorDia)}"
                  + (a.PercentualMulta == CalculadoraTributoEmAtraso.MultaMaxima ? ", limitada a 20%" : string.Empty)
                  + $"; {Formato.Moeda(a.Principal)} x {Formato.Percentual(a.PercentualMulta)} = {Formato.Moeda(a.Multa)}"),
            new("Juros de mora", JurosTexto(a, r))
        };

        var observacoes = new List<string>
        {
            "Multa de 0,33% por dia de atraso, limitada a 20%, e juros pela Selic acumulada do mês seguinte ao vencimento até o anterior ao pagamento, mais 1% no mês do pagamento (Lei 9.430/1996, art. 61). As mesmas regras valem para o DAS do Simples, o DAE do doméstico e a GPS (Lei 8.212/1991, art. 35).",
            "Informe o vencimento já prorrogado: quando ele cai em fim de semana ou feriado, a guia vence no dia útil seguinte, sem acréscimos.",
            "A Selic do mês anterior ao pagamento precisa estar na tabela Selic; se faltar, atualize a tabela pela internet. Para emitir a guia, use o Sicalc, o Portal do Simples Nacional ou o eSocial, que calculam os mesmos acréscimos.",
            "Não vale para o FGTS em atraso, que tem multa e juros próprios (Lei 8.036/1990, art. 22), nem para tributos estaduais e municipais."
        };

        return new DemonstrativoDto(
            $"{guia} em atraso",
            $"Vencimento em {Formato.Data(r.Vencimento)} • pagamento em {Formato.Data(r.Pagamento)}",
            [
                new("Total a pagar", Formato.Moeda(a.Total), a.DiasDeAtraso == 0 ? "Sem acréscimos" : $"{Formato.Moeda(a.Multa + a.Juros)} de acréscimos"),
                new("Multa", Formato.Moeda(a.Multa), Formato.Percentual(a.PercentualMulta)),
                new("Juros", Formato.Moeda(a.Juros), Formato.Percentual(a.PercentualJuros)),
                new("Atraso", Formato.Dias(a.DiasDeAtraso), $"Vencimento em {Formato.Data(r.Vencimento)}")
            ],
            [
                new($"Principal ({guia})", string.Empty, a.Principal),
                new("Multa de mora", Formato.Percentual(a.PercentualMulta), a.Multa),
                new("Juros de mora (Selic + 1%)", Formato.Percentual(a.PercentualJuros), a.Juros)
            ],
            [],
            [],
            [new GrupoMemoriaDto("Acréscimos de mora", $"Total: {Formato.Moeda(a.Total)}", formulas)],
            observacoes,
            RotuloProventos: "Composição da guia",
            RotuloResultado: "Total a pagar");
    }

    private static string JurosTexto(AcrescimosDeMora a, SimularTributoAtrasoRequest r)
    {
        if (a.DiasDeAtraso == 0)
            return Formato.Moeda(0m);
        if (a.PercentualJuros == 0m)
            return "Pago no próprio mês do vencimento: sem juros.";
        var selic = a.PrimeiroMesSelic is { } primeiro && a.UltimoMesSelic is { } ultimo
            ? $"Selic de {Formato.Competencia(primeiro)} a {Formato.Competencia(ultimo)} ({Formato.Percentual(a.SelicAcumulada)}) + 1% de {Formato.Competencia(new DateOnly(r.Pagamento.Year, r.Pagamento.Month, 1))}"
            : $"Pago no mês seguinte ao vencimento: só 1% de {Formato.Competencia(new DateOnly(r.Pagamento.Year, r.Pagamento.Month, 1))}";
        return $"{selic} = {Formato.Percentual(a.PercentualJuros)}; {Formato.Moeda(a.Principal)} x {Formato.Percentual(a.PercentualJuros)} = {Formato.Moeda(a.Juros)}";
    }

    public static string NomeGuia(GuiaDeRecolhimento guia) => guia switch
    {
        GuiaDeRecolhimento.Das => "DAS",
        GuiaDeRecolhimento.Dae => "DAE",
        GuiaDeRecolhimento.Gps => "GPS",
        _ => "DARF"
    };
}
