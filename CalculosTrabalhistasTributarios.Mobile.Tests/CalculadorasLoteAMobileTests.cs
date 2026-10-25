using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CalculadorasLoteAMobileTests
{
    [Fact]
    public async Task Abono_salarial_le_ano_meses_media_cadastro_e_limite()
    {
        var captura = new Captura<SimularAbonoSalarialRequest>();
        var vm = new AbonoSalarialMobileViewModel(captura, new PdfTeste(), NullLogger<AbonoSalarialMobileViewModel>.Instance);
        Assert.True(Assert.Single(vm.Campos, campo => campo.Chave == "cadastro").Marcado);
        Alterar(vm, "anoBase", "2024");
        Alterar(vm, "meses", "7");
        Alterar(vm, "media", "2.100,50");
        Marcar(vm, "cadastro", false);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new SimularAbonoSalarialRequest(2024, 7, 2100.50m, false, 0m), captura.Pedido);
    }

    [Fact]
    public async Task Afastamento_alterna_campos_da_doenca_e_das_licencas()
    {
        var captura = new Captura<SimularAfastamentoRequest>();
        var vm = new AfastamentoMobileViewModel(captura, new PdfTeste(), NullLogger<AfastamentoMobileViewModel>.Instance);
        Assert.False(Visivel(vm, "fimAnterior"));
        Assert.False(Visivel(vm, "empresaCidada"));
        Alterar(vm, "inicio", "10/03/2026");
        Alterar(vm, "dias", "40");
        Alterar(vm, "remuneracao", "3.000,00");
        Alterar(vm, "media", "2.800,00");
        Opcao(vm, "anterior", 1);
        Assert.True(Visivel(vm, "fimAnterior"));
        Assert.True(Visivel(vm, "diasAnteriores"));
        Alterar(vm, "fimAnterior", "20/02/2026");
        Alterar(vm, "diasAnteriores", "10");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(TipoAfastamento.Doenca, captura.Pedido?.Tipo);
        Assert.Equal(new DateOnly(2026, 3, 10), captura.Pedido?.Inicio);
        Assert.Equal(40, captura.Pedido?.Dias);
        Assert.Equal(2800m, captura.Pedido?.Media);
        Assert.Equal(new AfastamentoAnterior(OrigemAfastamentoAnterior.Empresa, new DateOnly(2026, 2, 20), 10), captura.Pedido?.Anterior);

        Opcao(vm, "anterior", 2);
        Assert.False(Visivel(vm, "diasAnteriores"));
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new AfastamentoAnterior(OrigemAfastamentoAnterior.BeneficioInss, new DateOnly(2026, 2, 20), 0), captura.Pedido?.Anterior);

        Opcao(vm, "tipo", 2);
        Assert.False(Visivel(vm, "dias"));
        Assert.False(Visivel(vm, "media"));
        Assert.False(Visivel(vm, "anterior"));
        Assert.False(Visivel(vm, "fimAnterior"));
        Assert.True(Visivel(vm, "empresaCidada"));
        Marcar(vm, "empresaCidada", true);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(TipoAfastamento.Maternidade, captura.Pedido?.Tipo);
        Assert.Equal(0, captura.Pedido?.Dias);
        Assert.Equal(0m, captura.Pedido?.Media);
        Assert.True(captura.Pedido?.EmpresaCidada);
        Assert.Null(captura.Pedido?.Anterior);
    }

    [Fact]
    public async Task Carne_leao_encaminha_rendimentos_e_deducoes()
    {
        var captura = new Captura<SimularCarneLeaoRequest>();
        var vm = new CarneLeaoMobileViewModel(captura, new PdfTeste(), NullLogger<CarneLeaoMobileViewModel>.Instance);
        Alterar(vm, "competencia", "05/2026");
        Alterar(vm, "rendimentos", "8.000,00");
        Alterar(vm, "alugueis", "2.500,00");
        Alterar(vm, "despesasAluguel", "300,00");
        Alterar(vm, "livroCaixa", "1.200,00");
        Alterar(vm, "inss", "880,00");
        Alterar(vm, "dependentes", "2");
        Alterar(vm, "pensao", "500,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new SimularCarneLeaoRequest(new DateOnly(2026, 5, 1), 8000m, 2500m, 300m, 1200m, 880m, 2, 500m), captura.Pedido);
    }

    [Fact]
    public async Task Contribuicao_do_segurado_oculta_salario_fora_do_plano_normal()
    {
        var captura = new Captura<SimularContribuicaoSeguradoRequest>();
        var vm = new ContribuicaoSeguradoMobileViewModel(captura, new PdfTeste(), NullLogger<ContribuicaoSeguradoMobileViewModel>.Instance);
        Assert.True(Visivel(vm, "salario"));
        Alterar(vm, "competencia", "02/2026");
        Alterar(vm, "salario", "4.000,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new SimularContribuicaoSeguradoRequest(new DateOnly(2026, 2, 1), CategoriaSegurado.ContribuinteIndividual,
            PlanoContribuicao.Normal, PeriodicidadeContribuicao.Mensal, 4000m), captura.Pedido);

        Opcao(vm, "categoria", 1);
        Opcao(vm, "plano", 2);
        Opcao(vm, "periodicidade", 1);
        Assert.False(Visivel(vm, "salario"));
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new SimularContribuicaoSeguradoRequest(new DateOnly(2026, 2, 1), CategoriaSegurado.Facultativo,
            PlanoContribuicao.BaixaRenda, PeriodicidadeContribuicao.Trimestral, 0m), captura.Pedido);
    }

    [Fact]
    public async Task Correcao_de_valor_converte_a_opcao_no_indice_economico_correto()
    {
        var captura = new Captura<SimularCorrecaoValorRequest>();
        var vm = new CorrecaoValorMobileViewModel(captura, new PdfTeste(), NullLogger<CorrecaoValorMobileViewModel>.Instance);
        Alterar(vm, "valor", "10.000,00");
        Alterar(vm, "inicio", "01/2024");
        Alterar(vm, "fim", "01/2026");
        Alterar(vm, "juros", "1");
        Alterar(vm, "multa", "2");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new SimularCorrecaoValorRequest(10000m, new DateOnly(2024, 1, 1), new DateOnly(2026, 1, 1), IndiceEconomico.Ipca, 1m, 2m), captura.Pedido);

        var esperados = new[] { IndiceEconomico.Ipca, IndiceEconomico.Inpc, IndiceEconomico.IpcaE, IndiceEconomico.Selic, IndiceEconomico.Tr, IndiceEconomico.TaxaLegal };
        for (var indice = 0; indice < esperados.Length; indice++)
        {
            Opcao(vm, "indice", indice);
            await vm.CalcularCommand.ExecuteAsync(null);
            Assert.Equal(esperados[indice], captura.Pedido?.Indice);
        }
    }

    [Fact]
    public async Task Credito_do_trabalhador_alterna_iof_objetivo_e_preserva_margem_vazia()
    {
        var captura = new Captura<SimularCreditoTrabalhadorRequest>();
        var vm = new CreditoTrabalhadorMobileViewModel(captura, new PdfTeste(), NullLogger<CreditoTrabalhadorMobileViewModel>.Instance);
        Assert.False(Visivel(vm, "iof"));
        Assert.True(Visivel(vm, "dataLiberacao"));
        Alterar(vm, "competencia", "03/2026");
        Alterar(vm, "salario", "5.000,00");
        Alterar(vm, "existentes", "300,00");
        Alterar(vm, "valor", "10.000,00");
        Alterar(vm, "prazo", "36");
        Alterar(vm, "juros", "2,5");
        Alterar(vm, "dataLiberacao", "10/03/2026");
        Alterar(vm, "primeiroVencimento", "10/04/2026");
        Alterar(vm, "pagasParaQuitar", "5");
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = Assert.IsType<SimularCreditoTrabalhadorRequest>(captura.Pedido);
        Assert.Equal(new DateOnly(2026, 3, 1), pedido.Competencia);
        Assert.Equal(5000m, pedido.RemuneracaoHabitual);
        Assert.Equal(5, pedido.ParcelasPagasParaQuitacao);
        Assert.Equal(new EntradaCreditoTrabalhador(ObjetivoCreditoTrabalhador.ValorDesejado, 10000m, 36, 2.5m, 0m, 0m, 0m, 0m, 300m,
            null, true, new DateOnly(2026, 3, 10), new DateOnly(2026, 4, 10)), pedido.Credito);

        Opcao(vm, "objetivo", 1);
        Opcao(vm, "modoIof", 1);
        Assert.False(Visivel(vm, "valor"));
        Assert.True(Visivel(vm, "iof"));
        Assert.False(Visivel(vm, "dataLiberacao"));
        Assert.False(Visivel(vm, "primeiroVencimento"));
        Alterar(vm, "iof", "150,00");
        Alterar(vm, "margem", "0,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new EntradaCreditoTrabalhador(ObjetivoCreditoTrabalhador.LimitePelaMargem, 0m, 36, 2.5m, 150m, 0m, 0m, 0m, 300m,
            0m, false, null, null), captura.Pedido?.Credito);

        captura.Limpar();
        Alterar(vm, "margem", "abc");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Contains("Margem livre oficial", vm.Mensagem);
    }

    [Fact]
    public async Task Dividendos_le_valores_e_residencia_no_exterior()
    {
        var captura = new Captura<SimularDividendosRequest>();
        var vm = new DividendosMobileViewModel(captura, new PdfTeste(), NullLogger<DividendosMobileViewModel>.Instance);
        Alterar(vm, "competencia", "02/2026");
        Alterar(vm, "valor", "80.000,00");
        Alterar(vm, "transicao", "10.000,00");
        Alterar(vm, "jaRetido", "1.000,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new SimularDividendosRequest(new DateOnly(2026, 2, 1), 80000m, 10000m, 1000m, false), captura.Pedido);

        Opcao(vm, "residencia", 1);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.True(captura.Pedido?.Exterior);
    }

    [Fact]
    public async Task Domestico_le_salario_faltas_e_vale_transporte()
    {
        var captura = new Captura<SimularDomesticoRequest>();
        var vm = new DomesticoMobileViewModel(captura, new PdfTeste(), NullLogger<DomesticoMobileViewModel>.Instance);
        Alterar(vm, "competencia", "04/2026");
        Alterar(vm, "salario", "1.800,00");
        Alterar(vm, "adicionais", "150,00");
        Alterar(vm, "faltas", "2");
        Alterar(vm, "dependentes", "1");
        Alterar(vm, "valeTransporte", "220,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new SimularDomesticoRequest(new DateOnly(2026, 4, 1), 1800m, 150m, 2, 1, 220m), captura.Pedido);

        captura.Limpar();
        Alterar(vm, "faltas", "1,5");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Contains("Faltas", vm.Mensagem);
    }

    private static void Alterar(CalculadoraTrabalhistaViewModel vm, string chave, string valor) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave).Valor = valor;

    private static void Opcao(CalculadoraTrabalhistaViewModel vm, string chave, int indice) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave).Indice = indice;

    private static void Marcar(CalculadoraTrabalhistaViewModel vm, string chave, bool marcado) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave).Marcado = marcado;

    private static bool Visivel(CalculadoraTrabalhistaViewModel vm, string chave) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave).Visivel;

    private sealed class Captura<T> : ISimularDemonstrativoUseCase<T>
    {
        public T? Pedido { get; private set; }
        public DemonstrativoDto? Resposta { get; init; }
        public void Limpar() => Pedido = default;
        public Task<Result<DemonstrativoDto>> ExecutarAsync(T request, CancellationToken cancellationToken)
        {
            Pedido = request;
            return Task.FromResult<Result<DemonstrativoDto>>(Resposta is { } demonstrativo
                ? demonstrativo : Erro.Validacao("Capturado para teste."));
        }
    }

    private sealed class PdfTeste : ICompartilhamentoPdf
    {
        public Task CompartilharResumoAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
        public DemonstrativoDto? Ultimo { get; private set; }
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken)
        {
            Ultimo = demonstrativo;
            return Task.CompletedTask;
        }
    }
}
