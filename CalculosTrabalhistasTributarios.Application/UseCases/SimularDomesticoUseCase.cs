using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Folha do mês do empregado doméstico e o DAE do eSocial, que reúne num só documento o INSS e o IRRF descontados do
/// empregado e os encargos do empregador: 8% de contribuição patronal, 0,8% de GILRAT, 8% de FGTS e 3,2% de indenização
/// compensatória (LC 150/2015, arts. 34 e 35).
/// </summary>
public sealed class SimularDomesticoUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularDomesticoRequest>
{
    private const decimal Patronal = 8m;
    private const decimal Gilrat = 0.8m;
    private const decimal Fgts = 8m;
    private const decimal Compensatoria = 3.2m;
    private const decimal LimiteValeTransporte = 6m;

    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularDomesticoRequest r, CancellationToken cancellationToken)
    {
        if (r.Salario <= 0m)
            return Erro.Validacao("Informe o salário do empregado doméstico.");
        if (r.Adicionais < 0m || r.Faltas < 0 || r.Dependentes < 0 || r.CustoValeTransporte < 0m)
            return Erro.Validacao("Os valores, as faltas e os dependentes não podem ser negativos.");
        if (r.Faltas > 30)
            return Erro.Validacao("As faltas não podem passar dos 30 dias do mês.");

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(r.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var tabelas = consultaTabelas.Valor;

        var faltas = Arredondar(r.Salario / 30m * r.Faltas);
        var remuneracao = r.Salario - faltas + r.Adicionais;
        var inss = tabelas.CalcularInss(remuneracao);
        var irrf = tabelas.CalcularIrrf(remuneracao, inss.Valor, r.Dependentes);
        var valeTransporte = Math.Min(Arredondar(r.Salario * LimiteValeTransporte / 100m), r.CustoValeTransporte);

        var patronal = Arredondar(remuneracao * Patronal / 100m);
        var gilrat = Arredondar(remuneracao * Gilrat / 100m);
        var fgts = Arredondar(remuneracao * Fgts / 100m);
        var compensatoria = Arredondar(remuneracao * Compensatoria / 100m);
        var encargos = patronal + gilrat + fgts + compensatoria;
        var dae = encargos + inss.Valor + irrf.Imposto;
        var custo = remuneracao + encargos + r.CustoValeTransporte - valeTransporte;
        var vencimento = r.Competencia.AddMonths(1).AddDays(6);

        var proventos = new List<VerbaDto> { new("Salário", "30 dias", r.Salario) };
        if (r.Adicionais > 0m) proventos.Add(new("Horas extras e adicionais", "", r.Adicionais));
        var descontos = new List<VerbaDto>();
        if (faltas > 0m) descontos.Add(new("Faltas", Formato.Dias(r.Faltas), faltas));
        descontos.Add(new("INSS", "", inss.Valor));
        descontos.Add(new(MemoriaTributaria.DescricaoIrrf("IRRF", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), irrf.Imposto));
        if (valeTransporte > 0m) descontos.Add(new("Vale-transporte", "6% do salário", valeTransporte));
        var liquido = proventos.Sum(verba => verba.Valor) - descontos.Sum(verba => verba.Valor);

        var informativos = new List<VerbaDto>
        {
            new("DAE: contribuição patronal", Formato.PercentualCurto(Patronal), patronal),
            new("DAE: GILRAT (seguro de acidente do trabalho)", Formato.Percentual(Gilrat), gilrat),
            new("DAE: FGTS", Formato.PercentualCurto(Fgts), fgts),
            new("DAE: indenização compensatória", Formato.Percentual(Compensatoria), compensatoria),
            new("DAE: INSS descontado do empregado", "", inss.Valor),
            new("DAE: IRRF descontado do empregado", "", irrf.Imposto),
            new("Total do DAE", $"Vence em {Formato.Data(vencimento)}", dae)
        };

        var formulas = new List<FormulaDto>
        {
            new("Remuneração do mês", faltas > 0m || r.Adicionais > 0m
                ? $"{Formato.Moeda(r.Salario)}{(faltas > 0m ? $" - {Formato.Moeda(faltas)} ({Formato.Dias(r.Faltas)} de falta)" : "")}{(r.Adicionais > 0m ? $" + {Formato.Moeda(r.Adicionais)} (adicionais)" : "")} = {Formato.Moeda(remuneracao)}"
                : $"{Formato.Moeda(remuneracao)}"),
            new("Encargos do empregador", $"{Formato.Moeda(remuneracao)} x (8% + 0,8% + 8% + 3,2%) = {Formato.Moeda(patronal)} + {Formato.Moeda(gilrat)} + {Formato.Moeda(fgts)} + {Formato.Moeda(compensatoria)} = {Formato.Moeda(encargos)}"),
            new("Total do DAE", $"{Formato.Moeda(encargos)} (encargos) + {Formato.Moeda(inss.Valor)} (INSS) + {Formato.Moeda(irrf.Imposto)} (IRRF) = {Formato.Moeda(dae)}"),
            new("Custo do empregador", $"{Formato.Moeda(remuneracao)} + {Formato.Moeda(encargos)}{(r.CustoValeTransporte > 0m ? $" + {Formato.Moeda(r.CustoValeTransporte - valeTransporte)} (vale-transporte além do desconto)" : "")} = {Formato.Moeda(custo)}")
        };
        if (r.CustoValeTransporte > 0m)
            formulas.Insert(1, new("Vale-transporte", $"Menor entre 6% do salário ({Formato.Moeda(Arredondar(r.Salario * LimiteValeTransporte / 100m))}) e o custo das passagens ({Formato.Moeda(r.CustoValeTransporte)}) = {Formato.Moeda(valeTransporte)}"));

        var observacoes = new List<string>
        {
            $"O DAE de {Formato.Competencia(r.Competencia)} vence em {Formato.Data(vencimento)}, dia 7 do mês seguinte; se não for dia útil, pague no dia útil anterior (LC 150/2015, art. 35, § 2º). Emita a guia no eSocial, que soma os mesmos valores.",
            "A indenização compensatória de 3,2% substitui a multa de 40% do FGTS: na dispensa sem justa causa ela vai para o empregado; na justa causa, no pedido de demissão e no fim do contrato a prazo, volta ao empregador.",
            "No mês do 13º e das férias, os encargos e os descontos sobre eles também entram no DAE; calcule as verbas nas calculadoras 13º salário e Férias. Na saída, use a Rescisão com o vínculo Empregado doméstico."
        };
        if (tabelas.SalarioMinimo is { } minimo && r.Salario < minimo)
            observacoes.Insert(0, $"O salário informado é menor que o salário mínimo de {Formato.Moeda(minimo)}: só é permitido na jornada parcial, proporcional às horas, ou se houver piso estadual maior, que prevalece.");
        if (irrf.RetencaoDispensada)
            observacoes.Add(MemoriaTributaria.DispensaDeRetencao(irrf.ImpostoCalculado, irrf.LimiteDispensa));

        return new DemonstrativoDto(
            "Empregado doméstico",
            $"Competência {Formato.Competencia(r.Competencia)} • DAE vence em {Formato.Data(vencimento)}",
            [
                new("Líquido do empregado", Formato.Moeda(liquido), "Salário menos os descontos"),
                new("Total do DAE", Formato.Moeda(dae), $"Vence em {Formato.Data(vencimento)}"),
                new("Encargos do empregador", Formato.Moeda(encargos), "20% da remuneração"),
                new("Custo do empregador", Formato.Moeda(custo), "Remuneração e encargos do mês")
            ],
            proventos,
            descontos,
            informativos,
            [
                new GrupoMemoriaDto("Folha e DAE", $"DAE: {Formato.Moeda(dae)}", formulas),
                MemoriaTributaria.Inss("INSS do empregado", inss, "remuneração"),
                MemoriaTributaria.Irrf("IRRF do empregado", irrf, "remuneração")
            ],
            observacoes);
    }

    private static decimal Arredondar(decimal valor) => CalculadoraTributacao.Arredondar(valor);
}
