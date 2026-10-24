using System.Globalization;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CalculadorasLoteFMobileTests
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    [Fact]
    public async Task Estabilidade_encaminha_pedido_e_monta_o_demonstrativo_do_resultado_apurado()
    {
        var simulacao = new SimulacaoEstabilidadeDto(69, 2, 10, 2, 2, 7000m, 500m, 500m, 166.67m, 600m, 240m, 100m, 9106.67m);
        var caso = new EstabilidadeFake { Resposta = simulacao };
        var vm = new EstabilidadeMobileViewModel(caso, new PdfTeste(), NullLogger<EstabilidadeMobileViewModel>.Instance);
        Alterar(vm, "media", "3.000,00");
        Alterar(vm, "diasBase", "30");
        Alterar(vm, "demissao", "10/01/2025");
        Alterar(vm, "fimEstabilidade", "20/03/2025");
        Alterar(vm, "complementos", "100,00");

        await vm.CalcularCommand.ExecuteAsync(null);

        Assert.Equal(new SimularEstabilidadeRequest(3000m, 30, new DateOnly(2025, 1, 10), new DateOnly(2025, 3, 20), 100m), caso.Pedido);
        var demonstrativo = Assert.IsType<DemonstrativoDto>(vm.Demonstrativo);
        Assert.Equal(simulacao.Total, demonstrativo.Resultado);
        Assert.Equal(Moeda(simulacao.Total), Assert.Single(demonstrativo.Destaques, destaque => destaque.Rotulo == "Total estimado").Valor);

        caso.Resposta = null;
        caso.Falha = Erro.Validacao("O fim da estabilidade deve ser posterior à data de demissão.");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(vm.Demonstrativo);
        Assert.Equal("O fim da estabilidade deve ser posterior à data de demissão.", vm.Mensagem);

        caso.Pedido = null;
        Alterar(vm, "demissao", "32/01/2025");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(caso.Pedido);
        Assert.Equal("Demissão (dd/mm/aaaa): informe uma data em dd/mm/aaaa.", vm.Mensagem);
    }

    [Fact]
    public void Demonstrativo_da_estabilidade_reorganiza_as_verbas_sem_recalcular()
    {
        var entrada = new EntradaEstabilidadeDto(3000m, 30, new DateOnly(2025, 1, 10), new DateOnly(2025, 3, 20), 100m);
        var simulacao = new SimulacaoEstabilidadeDto(69, 2, 10, 2, 2, 7000m, 500m, 500m, 166.67m, 600m, 240m, 100m, 9106.67m);

        var demonstrativo = DemonstrativoEstabilidade.Montar(simulacao, entrada);

        Assert.Equal(simulacao.Total, demonstrativo.TotalProventos);
        Assert.Empty(demonstrativo.Descontos);
        Assert.Equal(simulacao.Indenizacao, Assert.Single(demonstrativo.Proventos, verba => verba.Descricao == "Indenização").Valor);
        Assert.Equal("2 meses e 10 dias", Assert.Single(demonstrativo.Proventos, verba => verba.Descricao == "Indenização").Referencia);
        Assert.Equal(Moeda(840m), Assert.Single(demonstrativo.Destaques, destaque => destaque.Rotulo == "FGTS + multa").Valor);
        Assert.Equal(Moeda(666.67m), Assert.Single(demonstrativo.Destaques, destaque => destaque.Rotulo == "Férias + 1/3").Valor);
        Assert.Equal("Total estimado", demonstrativo.RotuloResultado);
        Assert.DoesNotContain(demonstrativo.Observacoes, observacao => observacao.Contains("difere"));

        // Um complemento com mais de duas casas: o total da apuração é arredondado, e a diferença é explicada.
        var arredondada = simulacao with { Complementos = 100.004m };
        var comDiferenca = DemonstrativoEstabilidade.Montar(arredondada, entrada with { Complementos = 100.004m });
        Assert.Contains(comDiferenca.Observacoes, observacao => observacao.Contains("difere"));
    }

    [Fact]
    public async Task Pensao_le_os_beneficiarios_na_ordem_e_identifica_a_linha_com_erro()
    {
        var caso = new PensaoFake();
        var vm = new PensaoMobileViewModel(caso, new PdfTeste(), NullLogger<PensaoMobileViewModel>.Instance);
        var grade = Assert.Single(vm.Grades, item => item.Chave == "beneficiarios");
        Assert.Single(grade.Linhas);
        Assert.False(CampoDe(vm, "ordem").Visivel);
        Assert.False(CampoDe(vm, "baseInss").Visivel);

        Alterar(vm, "competencia", "06/2025");
        Alterar(vm, "valorBruto", "5.000,00");
        Alterar(vm, "dependentes", "1");
        Alterar(vm, "outrosDescontos", "200,00");
        grade.Linhas[0].Campo("percentual").Valor = "20";
        grade.AdicionarCommand.Execute(null);
        Assert.True(CampoDe(vm, "ordem").Visivel);
        var segunda = grade.Linhas[1];
        segunda.Campo("nome").Valor = "Ana";
        segunda.Campo("base").Indice = 3;
        Assert.False(segunda.Campo("percentual").Visivel);
        Assert.True(segunda.Campo("valor").Visivel);
        segunda.Campo("valor").Valor = "800,00";
        Opcao(vm, "ordem", 1);

        await vm.CalcularCommand.ExecuteAsync(null);

        var pedido = Assert.IsType<SimularPensaoRequest>(caso.Pedido);
        Assert.Equal(new DateOnly(2025, 6, 1), pedido.Competencia);
        Assert.Equal(5000m, pedido.ValorBruto);
        Assert.Equal(5000m, pedido.BaseInss);
        Assert.Equal(1, pedido.Dependentes);
        Assert.Equal(200m, pedido.OutrosDescontos);
        Assert.True(pedido.Sucessiva);
        Assert.Collection(pedido.Beneficiarios,
            primeiro => Assert.Equal(new BeneficiarioPensao("Beneficiário 1", new RegraPensao(BasePensao.RendimentosLiquidos, 20m, 0m)), primeiro),
            segundo => Assert.Equal(new BeneficiarioPensao("Ana", new RegraPensao(BasePensao.ValorFixo, 0m, 800m)), segundo));

        CampoDe(vm, "brutoComoBaseInss").Marcado = false;
        Assert.True(CampoDe(vm, "baseInss").Visivel);
        Alterar(vm, "baseInss", "4.000,00");
        grade.Linhas.Remove(segunda);
        Assert.False(CampoDe(vm, "ordem").Visivel);
        await vm.CalcularCommand.ExecuteAsync(null);
        pedido = Assert.IsType<SimularPensaoRequest>(caso.Pedido);
        Assert.Equal(4000m, pedido.BaseInss);
        Assert.False(pedido.Sucessiva);

        caso.Pedido = null;
        grade.AdicionarCommand.Execute(null);
        grade.Linhas[1].Campo("base").Indice = 2;
        grade.Linhas[1].Campo("percentual").Valor = "1.500";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(caso.Pedido);
        Assert.Equal("Beneficiário 2: O percentual do salário mínimo deve estar entre 0 e 1.000 (150 para 1,5 salário mínimo).", vm.Mensagem);

        grade.Linhas[0].Campo("percentual").Valor = "abc";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal("Beneficiário 1: Percentual (%): informe um número, por exemplo 3.500,00.", vm.Mensagem);
    }

    [Fact]
    public void Demonstrativo_da_pensao_usa_a_modalidade_aplicada_e_o_comparativo()
    {
        var (simulacao, entrada) = PensaoValorFixo(4000m);

        var demonstrativo = DemonstrativoPensaoAlimenticia.Montar(simulacao, entrada);

        // A modalidade normal tem o menor IRRF: os totais seguem os valores dela, sem recalcular.
        Assert.Equal(4000m - 400m - 68.56m - 500m, demonstrativo.Resultado);
        Assert.Equal(Moeda(demonstrativo.Resultado), Assert.Single(demonstrativo.Destaques, destaque => destaque.Rotulo == "Líquido de quem paga").Valor);
        Assert.Equal(Moeda(75m), Assert.Single(demonstrativo.Destaques, destaque => destaque.Rotulo == "Economia de IRRF").Valor);
        Assert.Equal(500m, Assert.Single(demonstrativo.Descontos, verba => verba.Descricao == "Pensão alimentícia").Valor);
        var comparativo = Assert.IsType<TabelaComparativaDto>(demonstrativo.Comparativo);
        Assert.Equal(new[] { "Normal (aplicada)", "Simplificado" }, comparativo.Colunas);
        var total = Assert.Single(comparativo.Linhas, linha => linha.Destaque);
        Assert.Equal(new[] { Moeda(568.56m), Moeda(583.56m) }, total.Valores);
        Assert.Contains(demonstrativo.Memoria, grupo => grupo.Titulo == "Normal - iteração 1");
        Assert.Contains(demonstrativo.Memoria, grupo => grupo.Titulo == "Simplificado - iteração 1");
        Assert.Contains(simulacao.MensagemVantagem, demonstrativo.Observacoes);

        // Sem rendimentos, a pensão não sai de um salário: fica como informativo, e o líquido não é calculado.
        var (semRendimentos, entradaSemRendimentos) = PensaoValorFixo(0m);
        var informativo = DemonstrativoPensaoAlimenticia.Montar(semRendimentos, entradaSemRendimentos);
        Assert.False(informativo.TemVerbas);
        Assert.Equal(500m, Assert.Single(informativo.Informativos).Valor);
        Assert.Equal("—", Assert.Single(informativo.Destaques, destaque => destaque.Rotulo == "Líquido de quem paga").Valor);
    }

    [Fact]
    public async Task Pensao_em_atraso_gera_parcelas_preserva_pagos_e_encaminha_os_criterios()
    {
        var caso = new PensaoAtrasoFake { Resposta = AtrasoTeste() };
        var vm = new PensaoAtrasoMobileViewModel(caso, new PdfTeste(), NullLogger<PensaoAtrasoMobileViewModel>.Instance);
        var grade = Assert.Single(vm.Grades, item => item.Chave == "parcelas");
        Assert.True(CampoDe(vm, "valorPensao").Visivel);
        Assert.False(CampoDe(vm, "percentual").Visivel);
        Opcao(vm, "base", 1);
        Assert.False(CampoDe(vm, "valorPensao").Visivel);
        Assert.True(CampoDe(vm, "percentual").Visivel);
        Opcao(vm, "base", 0);
        Alterar(vm, "valorPensao", "1.000,00");
        Alterar(vm, "percentual", "abc");
        Alterar(vm, "diaVencimento", "10");
        Alterar(vm, "primeiraParcela", "01/2025");
        Alterar(vm, "ultimaParcela", "02/2025");

        await Assert.IsAssignableFrom<IAsyncRelayCommand>(grade.GerarCommand).ExecuteAsync(null);

        Assert.Equal(new GerarParcelasAtrasoRequest(BasePensao.ValorFixo, 1000m, 0m, new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1), 10), caso.Geracoes.Single());
        Assert.Equal(new[] { "01/2025", "02/2025" }, grade.Linhas.Select(linha => linha.Campo("mes").Valor));
        Assert.True(grade.TemAviso);
        grade.Linhas[1].Campo("pago").Valor = "300,00";

        // Mudar o valor desatualiza as parcelas: o cálculo gera de novo e mantém o pago do mesmo mês.
        Alterar(vm, "valorPensao", "1.100,00");
        Alterar(vm, "dataCalculo", "01/03/2025");
        Opcao(vm, "correcao", 1);
        Opcao(vm, "juros", 2);
        Opcao(vm, "acrescimos", 1);
        await vm.CalcularCommand.ExecuteAsync(null);

        Assert.Equal(2, caso.Geracoes.Count);
        Assert.Equal(1100m, caso.Geracoes[1].Valor);
        var pedido = Assert.IsType<SimularPensaoAtrasoRequest>(caso.Pedido);
        Assert.Equal(new DateOnly(2025, 3, 1), pedido.DataCalculo);
        Assert.Null(pedido.DataAjuizamento);
        Assert.Equal(CorrecaoMonetaria.Ipca, pedido.Correcao);
        Assert.Equal(JurosDeMora.TaxaLegal, pedido.Juros);
        Assert.Equal(AcrescimosPenhora.MultaEHonorarios, pedido.Acrescimos);
        Assert.Collection(pedido.Parcelas,
            primeira => Assert.Equal(new ParcelaPensaoInformada(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10), 1100m, 0m), primeira),
            segunda => Assert.Equal(new ParcelaPensaoInformada(new DateOnly(2025, 2, 1), new DateOnly(2025, 2, 10), 1100m, 300m), segunda));
        var demonstrativo = Assert.IsType<DemonstrativoDto>(vm.Demonstrativo);
        Assert.Equal(caso.Resposta.TotalComAcrescimos, demonstrativo.Resultado);

        caso.Pedido = null;
        Alterar(vm, "dataAjuizamento", "15/02/2025");
        grade.Linhas[0].Campo("devido").Valor = "abc";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(2, caso.Geracoes.Count);
        Assert.Null(caso.Pedido);
        Assert.Equal("Parcela 1: Devido (R$): informe um número, por exemplo 3.500,00.", vm.Mensagem);
    }

    [Fact]
    public void Demonstrativo_da_pensao_em_atraso_fecha_com_o_total_e_separa_os_ritos()
    {
        var simulacao = AtrasoTeste();

        var demonstrativo = DemonstrativoPensaoAtraso.Montar(simulacao);

        Assert.Equal(simulacao.TotalComAcrescimos, demonstrativo.Resultado);
        Assert.Equal(simulacao.TotalPago, Assert.Single(demonstrativo.Descontos).Valor);
        Assert.Equal(Moeda(simulacao.TotalComAcrescimos), Assert.Single(demonstrativo.Destaques, destaque => destaque.Rotulo == "Total atualizado").Valor);
        Assert.Equal(Moeda(simulacao.TotalPenhoraComAcrescimos), Assert.Single(demonstrativo.Destaques, destaque => destaque.Rotulo == "Rito da penhora").Valor);
        Assert.Equal("Taxa legal (desde 30/08/2024)", Assert.Single(demonstrativo.Destaques, destaque => destaque.Rotulo == "Juros de mora").Complemento);
        var ritos = Assert.IsType<TabelaComparativaDto>(demonstrativo.Comparativo);
        Assert.Equal(new[] { Moeda(simulacao.TotalPrisao), Moeda(simulacao.TotalPenhoraComAcrescimos) }, Assert.Single(ritos.Linhas, linha => linha.Destaque).Valores);
        Assert.Equal(new[] { "Critério do teste", "Observação do teste" }, demonstrativo.Observacoes);
        Assert.Equal(2, Assert.Single(demonstrativo.Memoria).Formulas.Count);
    }

    private static (SimulacaoPensaoDto Simulacao, EntradaPensaoDto Entrada) PensaoValorFixo(decimal valorBruto)
    {
        var regra = new RegraPensao(BasePensao.ValorFixo, 0m, 500m);
        var beneficiario = new PensaoBeneficiarioDto("Beneficiário", regra, 0m, 500m);
        var normal = new ModalidadePensaoDto("Normal", 68.56m, 0m, 68.56m, 500m, 568.56m, 1,
            [new IteracaoPensaoDto(1, 3100m, 15m, 396.44m, 68.56m, 0m, 68.56m, 0m, 500m, 500m, [beneficiario])], 400m, "INSS", true);
        var simplificada = new ModalidadePensaoDto("Simplificado", 83.56m, 0m, 83.56m, 500m, 583.56m, 1,
            [new IteracaoPensaoDto(1, 3200m, 15m, 396.44m, 83.56m, 0m, 83.56m, 0m, 500m, 0m, [beneficiario])], 607.20m, "desconto simplificado", false);
        var simulacao = new SimulacaoPensaoDto(400m, normal, simplificada, "O cálculo normal é mais vantajoso.", 143.56m) { AliquotaIrrfSemPensao = 15m };
        return (simulacao, new EntradaPensaoDto(new DateOnly(2025, 6, 1), valorBruto, valorBruto, 0, regra, 0m));
    }

    private static SimulacaoPensaoAtrasoDto AtrasoTeste() => new(
        [
            new ParcelaAtrasoDto(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10), 1000m, 0m, 1000m, 1.01m, 1010m, 2m, 20.20m, 1030.20m, false),
            new ParcelaAtrasoDto(new DateOnly(2025, 2, 1), new DateOnly(2025, 2, 10), 1000m, 300m, 700m, 1.005m, 703.50m, 1m, 7.04m, 710.54m, true)
        ],
        new DateOnly(2025, 3, 1), null, CorrecaoMonetaria.Ipca, JurosDeMora.TaxaLegal, new DateOnly(2025, 2, 1),
        ["Critério do teste"], ["Observação do teste"], Multa: 103.02m, Honorarios: 103.02m);

    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);

    private static CampoMobile CampoDe(CalculadoraTrabalhistaViewModel vm, string chave) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave);

    private static void Alterar(CalculadoraTrabalhistaViewModel vm, string chave, string valor) => CampoDe(vm, chave).Valor = valor;

    private static void Opcao(CalculadoraTrabalhistaViewModel vm, string chave, int indice) => CampoDe(vm, chave).Indice = indice;

    private sealed class EstabilidadeFake : ISimularEstabilidadeUseCase
    {
        public SimularEstabilidadeRequest? Pedido { get; set; }
        public SimulacaoEstabilidadeDto? Resposta { get; set; }
        public Erro Falha { get; set; } = Erro.Validacao("Capturado para teste.");

        public Result<SimulacaoEstabilidadeDto> Executar(SimularEstabilidadeRequest request)
        {
            Pedido = request;
            if (Resposta is { } simulacao) return simulacao;
            return Falha;
        }
    }

    private sealed class PensaoFake : ISimularPensaoUseCase
    {
        public SimularPensaoRequest? Pedido { get; set; }

        public Task<Result<SimulacaoPensaoDto>> ExecutarAsync(SimularPensaoRequest request, CancellationToken cancellationToken)
        {
            Pedido = request;
            return Task.FromResult<Result<SimulacaoPensaoDto>>(Erro.Validacao("Capturado para teste."));
        }
    }

    private sealed class PensaoAtrasoFake : ISimularPensaoAtrasoUseCase
    {
        public List<GerarParcelasAtrasoRequest> Geracoes { get; } = [];
        public SimularPensaoAtrasoRequest? Pedido { get; set; }
        public required SimulacaoPensaoAtrasoDto Resposta { get; init; }

        public Task<Result<IReadOnlyList<ParcelaPensaoInformada>>> GerarParcelasAsync(GerarParcelasAtrasoRequest request, CancellationToken cancellationToken)
        {
            Geracoes.Add(request);
            IReadOnlyList<ParcelaPensaoInformada> parcelas =
            [
                new(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, request.DiaVencimento), request.Valor, 0m),
                new(new DateOnly(2025, 2, 1), new DateOnly(2025, 2, request.DiaVencimento), request.Valor, 0m)
            ];
            return Task.FromResult(Result.Ok(parcelas));
        }

        public Task<Result<SimulacaoPensaoAtrasoDto>> CalcularAsync(SimularPensaoAtrasoRequest request, CancellationToken cancellationToken)
        {
            Pedido = request;
            return Task.FromResult<Result<SimulacaoPensaoAtrasoDto>>(Resposta);
        }
    }

    private sealed class PdfTeste : ICompartilhamentoPdf
    {
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
