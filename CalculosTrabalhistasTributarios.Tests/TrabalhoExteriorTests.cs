using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class TrabalhoExteriorTests
{
    private static EntradaTrabalhoExterior Entrada(VinculoTrabalhoExterior vinculo = VinculoTrabalhoExterior.EmpregoAssalariado,
        decimal remuneracao = 10_000m, decimal juros = 50m, decimal imposto = 2_000m) =>
        new(vinculo, remuneracao, juros, imposto, 10m, 5m, 20m, 1m, 5m, 4.8m);

    private static SimularTrabalhoExteriorRequest Pedido(EntradaTrabalhoExterior? valores = null, bool compensar = true,
        decimal livroCaixa = 0m) =>
        new(new DateOnly(2026, 10, 15), "Estados Unidos", "USD", valores ?? Entrada(), compensar, 0m, 0, 0m, livroCaixa);

    private static async Task<Result<DemonstrativoDto>> Simular(SimularTrabalhoExteriorRequest pedido) =>
        await new SimularTrabalhoExteriorUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(pedido, default);

    [Fact]
    public void Cotacoes_e_encargos_tem_memorias_separadas()
    {
        var a = ConversaoTrabalhoExterior.Calcular(Entrada()).Sucesso();
        Assert.Equal(5m, a.CotacaoFiscalReais);
        Assert.Equal(50_000m, a.RemuneracaoFiscal);
        Assert.Equal(250m, a.JurosRecebidosFiscal);
        Assert.Equal(50_000m, a.RendimentosTributaveis); // juros de mora de salário isentos
        Assert.Equal(10_000m, a.ImpostoExteriorFiscal);
        Assert.Equal(48_000m, a.RemuneracaoEfetiva);
        Assert.Equal(240m, a.JurosRecebidosEfetivos);
        Assert.Equal(9_600m, a.ImpostoExteriorEfetivo);
        Assert.Equal(48m, a.TaxaMoedaEfetiva);
        Assert.Equal(24m, a.JurosBancoEfetivos);
        Assert.Equal(38_548m, a.CreditoBanco); // 48.000 + 240 - 9.600 - 48 - 24 - 20
    }

    [Fact]
    public void Moeda_intermediaria_e_juros_de_servico_entram_na_base_fiscal()
    {
        var a = ConversaoTrabalhoExterior.Calcular(new EntradaTrabalhoExterior(
            VinculoTrabalhoExterior.ServicoPessoaFisica, 1_000m, 10m, 0m, 0m, 0m, 0m, 1.1m, 5m, 5.2m)).Sucesso();
        Assert.Equal(5.5m, a.CotacaoFiscalReais);
        Assert.Equal(5_500m, a.RemuneracaoFiscal);
        Assert.Equal(55m, a.JurosRecebidosFiscal);
        Assert.Equal(5_555m, a.RendimentosTributaveis);
        Assert.Equal(5_252m, a.CreditoBanco);
    }

    [Fact]
    public async Task Credito_limitado_e_liquido_reconciliado_com_valores_de_referencia()
    {
        // Tabela mensal de 2026: (50.000 - desconto simplificado 607,20) × 27,5% - 908,73 = 12.674,29.
        // IR exterior: 2.000 USD × R$ 5 = R$ 10.000. IR brasileiro: 2.674,29.
        var dto = (await Simular(Pedido())).Sucesso();
        Assert.Contains("R$ 10.000,00", dto.Memoria[2].Formulas.Single(f => f.Titulo == "Crédito admitido").Formula);
        Assert.Equal(2_674.29m, dto.Descontos.Single(d => d.Descricao == "Carnê-leão a recolher no Brasil").Valor);
        Assert.Equal(35_873.71m, dto.Resultado);
        Assert.Equal("R$ 38.548,00", dto.Destaques[1].Valor);
    }

    [Fact]
    public async Task Sem_confirmacao_nao_usa_imposto_estrangeiro_como_credito()
    {
        var dto = (await Simular(Pedido(compensar: false))).Sucesso();
        Assert.Equal(12_674.29m, dto.Descontos.Single(d => d.Descricao == "Carnê-leão a recolher no Brasil").Valor);
        Assert.Equal(25_873.71m, dto.Resultado);
    }

    [Fact]
    public async Task Credito_nunca_excede_imposto_brasileiro()
    {
        var entrada = Entrada(remuneracao: 10_000m, juros: 0m, imposto: 9_000m) with
        {
            TaxaTransferenciaMoeda = 0m, JurosCobradosMoeda = 0m, TaxaTransferenciaReais = 0m
        };
        var dto = (await Simular(Pedido(entrada))).Sucesso();
        Assert.Equal(0m, dto.Descontos.Single(d => d.Descricao == "Carnê-leão a recolher no Brasil").Valor);
        Assert.Equal(4_800m, dto.Resultado);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(100, 101, 0)]
    [InlineData(100, 0, -1)]
    [InlineData(100_000_001, 0, 0)]
    public void Valores_invalidos_nao_geram_resultado(decimal remuneracao, decimal imposto, decimal taxa) =>
        Assert.Equal(TipoErro.Validacao, ConversaoTrabalhoExterior.Calcular(
            Entrada(remuneracao: remuneracao, juros: 0m, imposto: imposto) with { TaxaTransferenciaReais = taxa }).Falha().Tipo);

    [Fact]
    public async Task Salario_rejeita_livro_caixa_e_servico_aceita()
    {
        Assert.Equal(TipoErro.Validacao, (await Simular(Pedido(livroCaixa: 500m))).Falha().Tipo);
        Assert.False((await Simular(Pedido(Entrada(VinculoTrabalhoExterior.ServicoPessoaFisica), livroCaixa: 500m))).Falhou);
    }

    [Fact]
    public void Formulario_reabre_com_valores_e_opcoes()
    {
        var novo = new CalculadoraTrabalhoExterior(null!, null!);
        novo.ImportarCampos(new Dictionary<string, string>
        {
            ["País de origem"] = "Portugal", ["Moeda (ISO)"] = "EUR", ["Vínculo"] = "Serviço como PF",
            ["Remuneração (moeda)"] = "2.000,00", ["Livro-caixa (R$)"] = "300,00"
        });
        var dados = DadosFormulario.DeJson(new DadosFormulario(novo.ExportarCampos()).ParaJson());
        var reaberto = new CalculadoraTrabalhoExterior(null!, null!);
        reaberto.ImportarCampos(dados.Campos);
        Assert.Equal(novo.ExportarCampos(), reaberto.ExportarCampos());
        Assert.True(reaberto.Campos.Single(c => c.Rotulo == "Livro-caixa (R$)").Visivel);
    }
}
