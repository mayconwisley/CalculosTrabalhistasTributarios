using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Custo mensal de um empregado para a empresa: salário, encargos sobre a folha, provisões de 13º e férias com os seus
/// encargos e benefícios. No Simples Nacional (anexos I a III e V) a contribuição patronal está incluída no DAS;
/// no anexo IV, a empresa recolhe à parte a contribuição de 20% e o RAT, mas não as contribuições a terceiros.
/// </summary>
public sealed class SimularCustoFuncionarioUseCase : ISimularDemonstrativoUseCase<SimularCustoFuncionarioRequest>
{
    private const decimal AliquotaPatronal = 20m;
    private const decimal AliquotaFgts = 8m;
    private const decimal HorasMensais = 220m;

    // O cálculo não consulta o banco: a tarefa só cumpre o contrato assíncrono da interface.
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularCustoFuncionarioRequest r, CancellationToken cancellationToken) => Task.FromResult(Simular(r));

    private static Result<DemonstrativoDto> Simular(SimularCustoFuncionarioRequest r)
    {
        if (r.Salario < 0m || r.Beneficios < 0m || r.Terceiros < 0m)
            return Erro.Validacao("O salário, os benefícios e as contribuições a terceiros não podem ser negativos.");
        if (r.Rat is < 1m or > 3m)
            return Erro.Validacao("O RAT deve estar entre 1% e 3%, conforme o grau de risco da atividade.");
        if (r.Fap is < 0.5m or > 2m)
            return Erro.Validacao("O FAP deve estar entre 0,5 e 2.");

        var pagaPatronal = r.Regime != RegimeTributario.SimplesNacional;
        var pagaTerceiros = r.Regime == RegimeTributario.LucroRealOuPresumido;
        var aliquotaPatronal = pagaPatronal ? AliquotaPatronal : 0m;
        var aliquotaRat = pagaPatronal ? r.Rat * r.Fap : 0m;
        var aliquotaTerceiros = pagaTerceiros ? r.Terceiros : 0m;
        var aliquotaEncargos = aliquotaPatronal + aliquotaRat + aliquotaTerceiros + AliquotaFgts;

        var patronal = Percentual(r.Salario, aliquotaPatronal);
        var rat = Percentual(r.Salario, aliquotaRat);
        var terceiros = Percentual(r.Salario, aliquotaTerceiros);
        var fgts = Percentual(r.Salario, AliquotaFgts);
        var provisao13 = r.IncluirProvisoes ? CalculadoraTributacao.Arredondar(r.Salario / 12m) : 0m;
        var provisaoFerias = r.IncluirProvisoes ? CalculadoraTributacao.Arredondar(r.Salario * 4m / 36m) : 0m;
        var encargosProvisoes = Percentual(provisao13 + provisaoFerias, aliquotaEncargos);

        var itens = new List<VerbaDto> { new("Salário", "", r.Salario) };
        if (patronal > 0m) itens.Add(new("INSS patronal", Formato.PercentualCurto(aliquotaPatronal), patronal));
        if (rat > 0m) itens.Add(new("RAT ajustado pelo FAP", Formato.Percentual(aliquotaRat), rat));
        if (terceiros > 0m) itens.Add(new("Contribuições a terceiros (Sistema S e outras)", Formato.PercentualCurto(aliquotaTerceiros), terceiros));
        itens.Add(new("FGTS", Formato.PercentualCurto(AliquotaFgts), fgts));
        if (r.IncluirProvisoes)
        {
            itens.Add(new("Provisão de 13º salário", "1/12", provisao13));
            itens.Add(new("Provisão de férias + 1/3", "1/12 + 1/3", provisaoFerias));
            itens.Add(new("Encargos sobre as provisões", Formato.Percentual(aliquotaEncargos), encargosProvisoes));
        }
        if (r.Beneficios > 0m) itens.Add(new("Benefícios", "", r.Beneficios));

        var custo = itens.Sum(item => item.Valor);
        // No mês das férias, o salário e os encargos dele saem da provisão: somar 12 meses de salário e a provisão inteira
        // contaria esse mês duas vezes. O ano tem 12 salários, o 13º e o 1/3 de férias, com os encargos.
        var salarioComEncargos = r.Salario + patronal + rat + terceiros + fgts;
        var custoAnual = r.IncluirProvisoes ? custo * 12m - salarioComEncargos : custo * 12m;
        var acrescimo = r.Salario == 0m ? 0m : (custo - r.Salario) / r.Salario * 100m;

        var formulas = new List<FormulaDto>
        {
            new("Regime", NomeRegime(r.Regime)),
            new("Alíquota de encargos sobre a folha", $"{string.Join(" + ", TermosAliquota(r, aliquotaPatronal, aliquotaRat, aliquotaTerceiros))} = {Formato.Percentual(aliquotaEncargos)}"),
            new("Encargos mensais", $"Soma das parcelas calculadas sobre {Formato.Moeda(r.Salario)}: {string.Join(" + ", new[] { patronal, rat, terceiros, fgts }.Where(valor => valor > 0m).Select(Formato.Moeda))} = {Formato.Moeda(patronal + rat + terceiros + fgts)}")
        };
        if (r.IncluirProvisoes)
        {
            formulas.Add(new("Provisão de 13º", $"{Formato.Moeda(r.Salario)} ÷ 12 = {Formato.Moeda(provisao13)}"));
            formulas.Add(new("Provisão de férias + 1/3", $"{Formato.Moeda(r.Salario)} ÷ 12 x 4/3 = {Formato.Moeda(provisaoFerias)}"));
            formulas.Add(new("Encargos sobre as provisões", $"({Formato.Moeda(provisao13)} + {Formato.Moeda(provisaoFerias)}) x {Formato.Percentual(aliquotaEncargos)} = {Formato.Moeda(encargosProvisoes)}"));
        }
        formulas.Add(new("Custo mensal", $"{string.Join(" + ", itens.Select(item => Formato.Moeda(item.Valor)))} = {Formato.Moeda(custo)}"));
        formulas.Add(new("Custo anual", r.IncluirProvisoes
            ? $"{Formato.Moeda(custo)} x 12 - {Formato.Moeda(salarioComEncargos)} (salário e encargos do mês de férias, que já estão na provisão) = {Formato.Moeda(custoAnual)}"
            : $"{Formato.Moeda(custo)} x 12 = {Formato.Moeda(custoAnual)}"));

        var demonstrativo = new DemonstrativoDto(
            "Custo do funcionário",
            NomeRegime(r.Regime),
            [
                new("Custo mensal", Formato.Moeda(custo), $"Salário de {Formato.Moeda(r.Salario)}"),
                new("Custo anual", Formato.Moeda(custoAnual), r.IncluirProvisoes ? "12 salários, 13º e férias + 1/3" : "12 meses, sem 13º e férias"),
                new("Acréscimo sobre o salário", Formato.Percentual(CalculadoraTributacao.Arredondar(acrescimo)), "Encargos, provisões e benefícios"),
                new("Custo por hora", Formato.Moeda(custo / HorasMensais), "Jornada de 220 horas mensais")
            ],
            itens,
            [],
            [],
            [new GrupoMemoriaDto("Custo do funcionário", $"Custo mensal: {Formato.Moeda(custo)}", formulas)],
            [
                "No Simples Nacional, anexos I a III e V, a contribuição patronal já está incluída no DAS; no anexo IV, a empresa recolhe à parte os 20% e o RAT, mas não as contribuições a terceiros.",
                "As provisões distribuem mês a mês o 13º e as férias com 1/3, pagos uma vez por ano. Não inclui a provisão da multa do FGTS em caso de dispensa.",
                "Confira o RAT e o FAP da empresa no eSocial ou com a contabilidade; as contribuições a terceiros podem variar conforme a atividade."
            ],
            RotuloProventos: "Custo",
            RotuloResultado: "Custo mensal total");
        return demonstrativo;
    }

    private static decimal Percentual(decimal valor, decimal aliquota) => CalculadoraTributacao.Arredondar(valor * aliquota / 100m);

    private static string NomeRegime(RegimeTributario regime) => regime switch
    {
        RegimeTributario.LucroRealOuPresumido => "Lucro Real ou Presumido",
        RegimeTributario.SimplesNacional => "Simples Nacional (anexos I, II, III e V)",
        _ => "Simples Nacional (anexo IV)"
    };

    // Só entram as contribuições que o regime paga: no Simples (anexos I a III e V), só o FGTS.
    private static IEnumerable<string> TermosAliquota(SimularCustoFuncionarioRequest r, decimal patronal, decimal rat, decimal terceiros)
    {
        if (patronal > 0m)
            yield return $"{Formato.PercentualCurto(patronal)} (patronal)";
        if (rat > 0m)
            yield return $"{Formato.Percentual(rat)} (RAT {Formato.PercentualCurto(r.Rat)} x FAP {r.Fap.ToString("0.0000", Formato.Cultura)})";
        if (terceiros > 0m)
            yield return $"{Formato.PercentualCurto(terceiros)} (terceiros)";
        yield return $"{Formato.PercentualCurto(AliquotaFgts)} (FGTS)";
    }
}
