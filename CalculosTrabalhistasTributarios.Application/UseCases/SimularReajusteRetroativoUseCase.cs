using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

public sealed class SimularReajusteRetroativoUseCase : ISimularDemonstrativoUseCase<SimularReajusteRetroativoRequest>
{
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularReajusteRetroativoRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultado = CalculadoraReajusteRetroativo.Calcular(request.Parcelas);
        if (resultado.Falhou)
            return Task.FromResult<Result<DemonstrativoDto>>(resultado.Erro);

        var calculo = resultado.Valor;
        var primeiro = calculo.Parcelas.Min(item => item.Parcela.Competencia);
        var ultimo = calculo.Parcelas.Max(item => item.Parcela.Competencia);
        var proventos = calculo.Parcelas.Where(item => item.Diferenca > 0m)
            .Select(item => new VerbaDto($"{Nome(item.Parcela.Tipo)} — {item.Parcela.Competencia:MM/yyyy}",
                item.Parcela.Tipo == TipoParcelaReajuste.Salario ? "Diferença do mês" : $"{item.Parcela.Quantidade} {(item.Parcela.Tipo == TipoParcelaReajuste.DecimoTerceiro ? "avos" : "dias")}", item.Diferenca)).ToArray();
        var memoria = calculo.Parcelas.Select(item => new GrupoMemoriaDto(
            $"{Nome(item.Parcela.Tipo)} — {item.Parcela.Competencia:MM/yyyy}",
            $"Diferença: {Formato.Moeda(item.Diferenca)}",
            [
                new("Já pago", Formato.Moeda(item.ValorPago)),
                new("Valor devido", Formato.Moeda(item.ValorDevido)),
                new("Diferença bruta", $"{Formato.Moeda(item.ValorDevido)} - {Formato.Moeda(item.ValorPago)} = {Formato.Moeda(item.Diferenca)}"),
                new("FGTS estimado", $"{Formato.Moeda(item.Diferenca)} × 8% = {Formato.Moeda(item.Fgts)}")
            ])).ToArray();

        var demonstrativo = new DemonstrativoDto(
            "Diferenças de reajuste retroativo",
            $"Competências {primeiro:MM/yyyy} a {ultimo:MM/yyyy}",
            [
                new("Diferença bruta", Formato.Moeda(calculo.TotalBruto), "Antes de descontos legais"),
                new("Salários", Formato.Moeda(calculo.Salario), "Competências mensais"),
                new("13º e férias", Formato.Moeda(calculo.DecimoTerceiro + calculo.Ferias), "Somente lançamentos informados"),
                new("FGTS estimado", Formato.Moeda(calculo.Fgts), "Depósito do empregador; não é desconto")
            ],
            proventos,
            [],
            [new("FGTS sobre as diferenças", "8% por lançamento", calculo.Fgts)],
            memoria,
            [
                "O salário mensal informado deve refletir o valor efetivamente pago e o devido em cada competência. Antecipações e promoções podem ser consideradas editando cada linha.",
                "13º e férias gozadas só entram quando adicionados como lançamentos próprios. Para férias, informe a base salarial da época do pagamento; o cálculo acrescenta o terço constitucional. Evite repetir a remuneração das férias na linha de salário do mesmo mês.",
                "O FGTS é estimado em 8% por lançamento de natureza remuneratória e não reduz o valor devido ao trabalhador. Situações com alíquota diferenciada exigem apuração própria.",
                "Este é um demonstrativo bruto. INSS, IRRF, atualização monetária, juros e eventuais diferenças rescisórias dependem da forma e da data do pagamento e não estão incluídos.",
                "Para diferenças decorrentes de instrumento coletivo, confira a competência de celebração e os períodos de referência no eSocial (S-1200/InfoPerAnt e S-2206)."
            ],
            RotuloProventos: "Diferenças brutas",
            RotuloResultado: "Total bruto das diferenças");
        return Task.FromResult<Result<DemonstrativoDto>>(demonstrativo);
    }

    private static string Nome(TipoParcelaReajuste tipo) => tipo switch
    {
        TipoParcelaReajuste.Salario => "Salário",
        TipoParcelaReajuste.DecimoTerceiro => "13º salário",
        TipoParcelaReajuste.FeriasGozadas => "Férias gozadas + 1/3",
        _ => "Parcela"
    };
}
