using CalculosTrabalhistasTributarios.Domain.Trabalhista.CustoFuncionario;
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
    private const decimal AliquotaFgtsPadrao = 8m;
    private const decimal AliquotaFgtsAprendiz = 2m;

    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularCustoFuncionarioRequest r, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Simular(r));
    }

    private static Result<DemonstrativoDto> Simular(SimularCustoFuncionarioRequest r)
    {
        var apuracao = CalculadoraCustoFuncionario.Calcular(new EntradaCustoFuncionario(r.Salario, r.Regime,
            r.Rat, r.Fap, r.Terceiros, r.Beneficios, r.IncluirProvisoes, r.Aprendiz, r.HorasMensais, r.BeneficiosDetalhados));
        if (apuracao.Falhou) return apuracao.Erro;
        var resultado = apuracao.Valor;
        var domestico = r.Regime == RegimeTributario.EmpregadorDomestico;
        var aliquotaPatronal = resultado.AliquotaPatronal;
        var aliquotaRat = resultado.AliquotaRat;
        var aliquotaTerceiros = resultado.AliquotaTerceiros;
        var aliquotaFgts = resultado.AliquotaFgts;
        var aliquotaCompensatoria = resultado.AliquotaCompensatoria;
        var aliquotaEncargos = resultado.AliquotaEncargos;
        var patronal = resultado.Patronal;
        var rat = resultado.Rat;
        var terceiros = resultado.Terceiros;
        var fgts = resultado.Fgts;
        var compensatoria = resultado.Compensatoria;
        var provisao13 = resultado.Provisao13;
        var provisaoFerias = resultado.ProvisaoFerias;
        var encargosProvisoes = resultado.EncargosProvisoes;

        var itens = new List<VerbaDto> { new("Salário", "", r.Salario) };
        if (patronal > 0m) itens.Add(new("INSS patronal", Formato.PercentualCurto(aliquotaPatronal), patronal));
        if (rat > 0m) itens.Add(new(domestico ? "GILRAT (seguro de acidente do trabalho)" : "RAT ajustado pelo FAP", Formato.Percentual(aliquotaRat), rat));
        if (terceiros > 0m) itens.Add(new("Contribuições a terceiros (Sistema S e outras)", Formato.PercentualCurto(aliquotaTerceiros), terceiros));
        itens.Add(new(r.Aprendiz ? "FGTS do jovem aprendiz" : "FGTS", Formato.PercentualCurto(aliquotaFgts), fgts));
        if (compensatoria > 0m) itens.Add(new("Indenização compensatória (no lugar da multa do FGTS)", Formato.Percentual(aliquotaCompensatoria), compensatoria));
        if (r.IncluirProvisoes)
        {
            itens.Add(new("Provisão de 13º salário", "1/12", provisao13));
            itens.Add(new("Provisão de férias + 1/3", "1/12 + 1/3", provisaoFerias));
            itens.Add(new("Encargos sobre as provisões", Formato.Percentual(aliquotaEncargos), encargosProvisoes));
        }
        if (r.Beneficios > 0m) itens.Add(new("Benefícios", "", r.Beneficios));

        foreach (var beneficio in r.BeneficiosDetalhados ?? [])
            itens.Add(new($"Benefício: {beneficio.Descricao}",
                $"{Formato.Moeda(beneficio.Custo)} − {Formato.Moeda(beneficio.ParticipacaoEmpregado)}", beneficio.CustoLiquido));
        var custo = resultado.TotalComReserva;
        var custoAnual = resultado.CustoAnual;
        var salarioComEncargos = resultado.SalarioComEncargos;

        var formulas = new List<FormulaDto>
        {
            new("Regime", NomeRegime(r.Regime)),
            new("Alíquota de encargos sobre a folha", $"{string.Join(" + ", TermosAliquota(r, aliquotaPatronal, aliquotaRat, aliquotaTerceiros, aliquotaCompensatoria))} = {Formato.Percentual(aliquotaEncargos)}"),
            new("Encargos mensais", $"Soma das parcelas calculadas sobre {Formato.Moeda(r.Salario)}: {string.Join(" + ", new[] { patronal, rat, terceiros, fgts, compensatoria }.Where(valor => valor > 0m).Select(Formato.Moeda))} = {Formato.Moeda(patronal + rat + terceiros + fgts + compensatoria)}")
        };
        if (r.IncluirProvisoes)
        {
            formulas.Add(new("Provisão de 13º", $"{Formato.Moeda(r.Salario)} ÷ 12 = {Formato.Moeda(provisao13)}"));
            formulas.Add(new("Provisão de férias + 1/3", $"{Formato.Moeda(r.Salario)} ÷ 12 x 4/3 = {Formato.Moeda(provisaoFerias)}"));
            formulas.Add(new("Encargos sobre as provisões", $"({Formato.Moeda(provisao13)} + {Formato.Moeda(provisaoFerias)}) x {Formato.Percentual(aliquotaEncargos)} = {Formato.Moeda(encargosProvisoes)}"));
        }
        formulas.Add(new("Desembolso + reserva", $"{string.Join(" + ", itens.Select(item => Formato.Moeda(item.Valor)))} = {Formato.Moeda(custo)}"));
        formulas.Add(new("Custo anual", r.IncluirProvisoes
            ? $"{Formato.Moeda(custo)} x 12 - {Formato.Moeda(salarioComEncargos)} (salário e encargos do mês de férias, que já estão na provisão) = {Formato.Moeda(custoAnual)}"
            : $"{Formato.Moeda(custo)} x 12 = {Formato.Moeda(custoAnual)}"));

        formulas.Add(new("Desembolso mensal recorrente", $"Salário e encargos + benefícios líquidos = {Formato.Moeda(resultado.Recorrente)}"));
        formulas.Add(new("Reserva mensal de provisões", $"13º + férias com 1/3 + encargos das provisões = {Formato.Moeda(resultado.Reserva)}"));
        formulas.Add(new("Custo médio mensal", $"{Formato.Moeda(custoAnual)} ÷ 12 = {Formato.Moeda(resultado.MediaMensal)}"));
        formulas.Add(new("Custo por hora contratual", $"{Formato.Moeda(resultado.MediaMensal)} ÷ {r.HorasMensais.ToString("0.##", Formato.Cultura)} horas = {Formato.Moeda(resultado.CustoHora)}"));
        formulas.Add(new("Acréscimo médio sobre o salário", resultado.Acrescimo is { } acrescimo
            ? $"({Formato.Moeda(resultado.MediaMensal)} − {Formato.Moeda(r.Salario)}) ÷ {Formato.Moeda(r.Salario)} × 100 = {Formato.Percentual(CalculadoraTributacao.Arredondar(acrescimo))}"
            : "Não calculado: o salário informado é zero."));

        var demonstrativo = new DemonstrativoDto(
            "Custo do funcionário",
            NomeRegime(r.Regime),
            [
                new("Desembolso recorrente", Formato.Moeda(resultado.Recorrente), "Mês normal: salário, encargos e benefícios"),
                new("Reserva de provisões", Formato.Moeda(resultado.Reserva), r.IncluirProvisoes ? "13º, férias + 1/3 e seus encargos" : "Provisões desativadas"),
                new("Custo anual", Formato.Moeda(custoAnual), r.IncluirProvisoes ? "Ano completo, sem duplicar o salário das férias" : "12 meses; exclui 13º e adicional de férias"),
                new("Custo médio mensal", Formato.Moeda(resultado.MediaMensal), "Custo anual dividido por 12"),
                new("Acréscimo médio sobre o salário", resultado.Acrescimo is { } percentual ? Formato.Percentual(CalculadoraTributacao.Arredondar(percentual)) : "Não calculado", "Compara a média mensal com o salário"),
                new("Custo por hora contratual", Formato.Moeda(resultado.CustoHora), $"Média mensal ÷ {r.HorasMensais.ToString("0.##", Formato.Cultura)} horas")
            ],
            itens,
            [],
            [],
            [new GrupoMemoriaDto("Custo do funcionário", $"Desembolso + reserva: {Formato.Moeda(custo)}", formulas)],
            (domestico
            ? new string[] {
                "O empregador doméstico recolhe num só DAE, com vencimento conforme a competência no eSocial, 8% de contribuição patronal, 0,8% de GILRAT, 8% de FGTS e 3,2% de indenização compensatória, além do INSS e do IRRF descontados do empregado (LC 150/2015, arts. 34 e 35). Para a guia do mês, use a calculadora Empregado doméstico (DAE).",
                "As provisões distribuem mês a mês o 13º e as férias com 1/3, pagos uma vez por ano. A indenização compensatória substitui a multa de 40% do FGTS na dispensa."
            }
            : new string[] {
                "No Simples Nacional, anexos I a III e V, a contribuição patronal já está incluída no DAS; no anexo IV, a empresa recolhe à parte os 20% e o RAT, mas não as contribuições a terceiros.",
                "As provisões distribuem mês a mês o 13º e as férias com 1/3, pagos uma vez por ano. Não inclui a provisão da multa do FGTS em caso de dispensa.",
                "Confira o RAT e o FAP da empresa no eSocial ou com a contabilidade; as contribuições a terceiros podem variar conforme a atividade."
            }).Concat(new[]
            {
                "Estimativa para um ano completo, com salário constante e 30 dias de férias, sem substituto. Benefícios são considerados durante 12 meses e informados líquidos da participação do empregado, sem incidências de folha nesta simulação.",
                "Desembolso + reserva não é a média mensal anualizada: a reserva inclui o salário das férias, que substitui um salário ordinário no ano. O custo por hora é contratual, não por hora efetivamente trabalhada.",
                "As provisões e seus encargos são arredondados mensalmente; a projeção anual preserva esses centavos e pode diferir de uma apuração direta anual. A média e a hora usam precisão integral antes da exibição.",
                "Não contempla desoneração/reoneração da folha, atividades concomitantes do Simples, adicionais, horas extras ou custos de substituição e rescisão."
            }).ToArray(),
            RotuloProventos: "Custo",
            RotuloResultado: "Desembolso + reserva mensal");
        return demonstrativo;
    }

    private static string NomeRegime(RegimeTributario regime) => regime switch
    {
        RegimeTributario.LucroRealOuPresumido => "Lucro Real ou Presumido",
        RegimeTributario.SimplesNacional => "Simples Nacional (anexos I, II, III e V)",
        RegimeTributario.EmpregadorDomestico => "Empregador doméstico",
        _ => "Simples Nacional (anexo IV)"
    };

    // Só entram as contribuições que o regime paga: no Simples (anexos I a III e V), só o FGTS.
    private static IEnumerable<string> TermosAliquota(SimularCustoFuncionarioRequest r, decimal patronal, decimal rat, decimal terceiros, decimal compensatoria)
    {
        if (r.Regime == RegimeTributario.EmpregadorDomestico)
        {
            yield return $"{Formato.PercentualCurto(patronal)} (patronal)";
            yield return $"{Formato.Percentual(rat)} (GILRAT)";
            yield return $"{Formato.PercentualCurto(AliquotaFgtsPadrao)} (FGTS)";
            yield return $"{Formato.Percentual(compensatoria)} (indenização compensatória)";
            yield break;
        }
        if (patronal > 0m)
            yield return $"{Formato.PercentualCurto(patronal)} (patronal)";
        if (rat > 0m)
            yield return $"{Formato.Percentual(rat)} (RAT {Formato.PercentualCurto(r.Rat)} x FAP {r.Fap.ToString("0.0000", Formato.Cultura)})";
        if (terceiros > 0m)
            yield return $"{Formato.PercentualCurto(terceiros)} (terceiros)";
        yield return $"{Formato.PercentualCurto(r.Aprendiz ? AliquotaFgtsAprendiz : AliquotaFgtsPadrao)} (FGTS)";
    }
}
