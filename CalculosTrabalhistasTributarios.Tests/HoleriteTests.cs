using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Tests.Referencia;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class HoleriteTests
{
    private static readonly DateOnly Outubro2026 = new(2026, 10, 1);

    private static SimularHoleriteRequest Pedido(decimal salario = 3000m, GrauInsalubridade insalubridade = GrauInsalubridade.Nenhum, bool periculosidade = false,
        decimal horas50 = 0m, int feriados = 0, int faltas = 0, int descansos = 0, decimal atrasos = 0m, int filhos = 0, RegraPensao? pensao = null,
        decimal valeTransporte = 0m, decimal adiantamento = 0m, decimal outrosDescontos = 0m, int dependentes = 0, decimal premios = 0m, decimal naoTributaveis = 0m, decimal previdencia = 0m) =>
        new(Outubro2026, salario, insalubridade, periculosidade, 0m, 220m, horas50, 50m, 0m, 100m, 0m, 20m, feriados, faltas, descansos, atrasos, dependentes, filhos, pensao,
            valeTransporte, adiantamento, outrosDescontos, Premios: premios, ProventosNaoTributaveis: naoTributaveis, PrevidenciaComplementar: previdencia);

    private static async Task<Result<DemonstrativoDto>> ResultadoAsync(SimularHoleriteRequest request) =>
        await new SimularHoleriteUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(request, default);

    private static Task<DemonstrativoDto> CalcularAsync(SimularHoleriteRequest request) => ResultadoAsync(request).Sucesso();

    private static decimal Valor(IEnumerable<VerbaDto> verbas, string inicio) => verbas.Single(verba => verba.Descricao.StartsWith(inicio)).Valor;

    [Fact]
    public async Task Horas_faltas_atrasos_e_descontos_no_mesmo_holerite()
    {
        // 10/2026 tem 4 domingos e o feriado de 12/10: 26 dias úteis e 5 de descanso. Insalubridade média: 20% de 1.621,00.
        var resultado = await CalcularAsync(Pedido(insalubridade: GrauInsalubridade.Medio, horas50: 10m, feriados: 1, faltas: 2, descansos: 1, atrasos: 1.5m,
            valeTransporte: 300m, adiantamento: 1000m, outrosDescontos: 50m));

        Assert.Equal(324.20m, Valor(resultado.Proventos, "Adicional de insalubridade"));
        Assert.Equal(226.65m, Valor(resultado.Proventos, "Horas extras 50%"));     // 3.324,20 ÷ 220 x 150% x 10h
        Assert.Equal(43.59m, Valor(resultado.Proventos, "DSR"));                   // 226,65 ÷ 26 x 5
        Assert.Equal(221.61m, Valor(resultado.Descontos, "Faltas"));               // 3.324,20 ÷ 30 x 2
        Assert.Equal(110.81m, Valor(resultado.Descontos, "DSR perdido"));          // 3.324,20 ÷ 30 x 1
        Assert.Equal(22.67m, Valor(resultado.Descontos, "Atrasos"));               // 3.324,20 ÷ 220 x 1,5h
        Assert.Equal(180m, Valor(resultado.Descontos, "Vale-transporte"));         // 6% de 3.000,00, menor que o custo de 300,00

        // INSS e IRRF sobre a remuneração depois das faltas e dos atrasos, conferidos com o modelo independente.
        const decimal remuneracao = 3324.20m + 226.65m + 43.59m - 221.61m - 110.81m - 22.67m;
        var modelo = ModeloTributario.Calcular(Outubro2026, remuneracao, 0);
        Assert.Equal(modelo.Inss, Valor(resultado.Descontos, "INSS"));
        Assert.Equal(modelo.IrrfRetido, Valor(resultado.Descontos, "IRRF"));
        Assert.Equal(ModeloTributario.Arredondar(remuneracao * .08m), Valor(resultado.Informativos, "FGTS"));
        Assert.Equal(120m, Valor(resultado.Informativos, "Vale-transporte pago pela empresa"));
        Assert.Equal(3594.44m - 221.61m - 110.81m - 22.67m - modelo.Inss - modelo.IrrfRetido - 180m - 1000m - 50m, resultado.Resultado);
    }

    [Fact]
    public async Task Periculosidade_e_insalubridade_nao_se_acumulam()
    {
        var resultado = await CalcularAsync(Pedido(insalubridade: GrauInsalubridade.Maximo, periculosidade: true));
        // 30% de 3.000,00 = 900,00 contra 40% de 1.621,00 = 648,40.
        Assert.Equal(900m, Valor(resultado.Proventos, "Adicional de periculosidade"));
        Assert.DoesNotContain(resultado.Proventos, verba => verba.Descricao.StartsWith("Adicional de insalubridade"));
        Assert.Contains(resultado.Observacoes, observacao => observacao.Contains("não se acumulam"));
    }

    [Fact]
    public async Task Salario_familia_pela_remuneracao_do_mes()
    {
        var comDireito = await CalcularAsync(Pedido(salario: 1621m, filhos: 2));
        Assert.Equal(135.08m, Valor(comDireito.Proventos, "Salário-família"));    // 2 x 67,54
        var semDireito = await CalcularAsync(Pedido(salario: 2500m, filhos: 2));
        Assert.DoesNotContain(semDireito.Proventos, verba => verba.Descricao == "Salário-família");
    }

    [Fact]
    public async Task Pensao_sobre_o_liquido_deduzida_do_irrf()
    {
        var resultado = await CalcularAsync(Pedido(salario: 8500m, pensao: new RegraPensao(BasePensao.RendimentosLiquidos, 30m, 0m)));
        var inss = Valor(resultado.Descontos, "INSS");
        var irrf = Valor(resultado.Descontos, "IRRF");
        var pensao = Valor(resultado.Descontos, "Pensão alimentícia");
        // Estável: a pensão é 30% do líquido calculado com ela mesma deduzida da base do IRRF.
        Assert.Equal(ModeloTributario.Arredondar((8500m - inss - irrf) * .30m), pensao);
        Assert.True(irrf < ModeloTributario.Calcular(Outubro2026, 8500m, 0).IrrfRetido);
    }

    [Fact]
    public async Task Dependentes_reduzem_o_irrf()
    {
        var semDependentes = await CalcularAsync(Pedido(salario: 6000m));
        var comDependentes = await CalcularAsync(Pedido(salario: 6000m, dependentes: 2));

        Assert.Equal(ModeloTributario.Calcular(Outubro2026, 6000m, 0).IrrfRetido, Valor(semDependentes.Descontos, "IRRF"));
        Assert.Equal(ModeloTributario.Calcular(Outubro2026, 6000m, 2).IrrfRetido, Valor(comDependentes.Descontos, "IRRF"));
        Assert.True(Valor(comDependentes.Descontos, "IRRF") < Valor(semDependentes.Descontos, "IRRF"));
        Assert.Contains(comDependentes.Memoria.Single(grupo => grupo.Titulo == "IRRF").Formulas, formula => formula.Formula.Contains("2 x") && formula.Formula.Contains("(dependentes)"));
    }

    [Fact]
    public async Task Premios_so_no_irrf_e_nao_tributaveis_so_no_liquido()
    {
        var resultado = await CalcularAsync(Pedido(salario: 8000m, premios: 2000m, naoTributaveis: 500m));

        // INSS e FGTS só sobre o salário; o IRRF sobre o salário e os prêmios, com o INSS do salário deduzido.
        var inss = ModeloTributario.Calcular(Outubro2026, 8000m, 0).Inss;
        var normal = ModeloTributario.Arredondar((10000m - inss) * .275m - 908.73m);
        var simplificado = ModeloTributario.Arredondar((10000m - 607.20m) * .275m - 908.73m);
        Assert.Equal(inss, Valor(resultado.Descontos, "INSS"));
        Assert.Equal(Math.Min(normal, simplificado), Valor(resultado.Descontos, "IRRF"));
        Assert.Equal(640m, Valor(resultado.Informativos, "FGTS"));
        Assert.Equal(8000m, Valor(resultado.Informativos, "Base do INSS e do FGTS"));
        Assert.Equal(10000m - inss, Valor(resultado.Informativos, "Base do IRRF"));

        Assert.Equal(2000m, Valor(resultado.Proventos, "Prêmios"));
        Assert.Equal(500m, Valor(resultado.Proventos, "Proventos não tributáveis"));
        Assert.Equal(8000m + 2000m + 500m - inss - Math.Min(normal, simplificado), resultado.Resultado);
    }

    [Fact]
    public async Task Previdencia_complementar_deduzida_por_inteiro_nas_deducoes_legais()
    {
        var resultado = await CalcularAsync(Pedido(salario: 8000m, previdencia: 800m));

        // Sem o limite de 12% na fonte: os 800,00 (10%) saem inteiros da base; o INSS e o FGTS não mudam.
        var inss = ModeloTributario.Calcular(Outubro2026, 8000m, 0).Inss;
        Assert.Equal(inss, Valor(resultado.Descontos, "INSS"));
        Assert.Equal(ModeloTributario.Arredondar((8000m - inss - 800m) * .275m - 908.73m), Valor(resultado.Descontos, "IRRF (deduções legais)"));
        Assert.Equal(800m, Valor(resultado.Descontos, "Previdência complementar"));
        Assert.Equal(640m, Valor(resultado.Informativos, "FGTS"));
        Assert.Equal(8000m - inss - 800m, Valor(resultado.Informativos, "Base do IRRF"));
        Assert.Contains(resultado.Memoria.Single(grupo => grupo.Titulo == "IRRF").Formulas, formula => formula.Formula.Contains("(previdência complementar)"));
    }

    [Fact]
    public async Task Previdencia_complementar_sem_efeito_quando_o_simplificado_e_melhor()
    {
        // 5.500,00 - 571,50 de INSS - 30,00 = 4.898,50, contra 4.892,80 no simplificado: vale o simplificado, que não deduz a previdência.
        var sem = await CalcularAsync(Pedido(salario: 5500m));
        var com = await CalcularAsync(Pedido(salario: 5500m, previdencia: 30m));

        Assert.Equal(Valor(sem.Descontos, "IRRF (desconto simplificado)"), Valor(com.Descontos, "IRRF (desconto simplificado)"));
        Assert.Equal(sem.Resultado - 30m, com.Resultado);
        Assert.Contains(com.Memoria.Single(grupo => grupo.Titulo == "IRRF").Formulas, formula => formula.Formula.Contains("a previdência complementar não é deduzida nesta modalidade"));
    }

    [Fact]
    public async Task Proventos_nao_tributaveis_nao_mudam_impostos_nem_salario_familia()
    {
        var sem = await CalcularAsync(Pedido(salario: 1621m, filhos: 1));
        var com = await CalcularAsync(Pedido(salario: 1621m, filhos: 1, naoTributaveis: 1000m));

        Assert.Equal(Valor(sem.Descontos, "INSS"), Valor(com.Descontos, "INSS"));
        Assert.Equal(Valor(sem.Descontos, "IRRF"), Valor(com.Descontos, "IRRF"));
        Assert.Equal(Valor(sem.Proventos, "Salário-família"), Valor(com.Proventos, "Salário-família"));
        Assert.Equal(sem.Resultado + 1000m, com.Resultado);
    }

    [Fact]
    public async Task Barra_descansos_faltas_e_descontos_fora_do_limite()
    {
        await ResultadoAsync(Pedido(descansos: 6)).Falha();
        await ResultadoAsync(Pedido(faltas: 31)).Falha();
        await ResultadoAsync(Pedido(faltas: 26, descansos: 5)).Falha();
        await ResultadoAsync(Pedido(adiantamento: 5000m)).Falha();
        await ResultadoAsync(Pedido(premios: -1m)).Falha();
        await ResultadoAsync(Pedido(naoTributaveis: -1m)).Falha();
        await ResultadoAsync(Pedido(previdencia: -1m)).Falha();
    }
}
