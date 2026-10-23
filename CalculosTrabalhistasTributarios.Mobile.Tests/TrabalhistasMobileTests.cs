using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class TrabalhistasMobileTests
{
    [Fact]
    public async Task Decimo_terceiro_encaminha_parcelas_e_complemento_aos_casos_corretos()
    {
        var parcelas = new Captura<SimularDecimoTerceiroRequest>();
        var complemento = new Captura<SimularComplementoDecimoTerceiroRequest>();
        var vm = new DecimoTerceiroMobileViewModel(parcelas, complemento, new PdfTeste(), NullLogger<DecimoTerceiroMobileViewModel>.Instance);
        Alterar(vm, "competencia", "12/2025");
        Alterar(vm, "salario", "3.500,00");
        Alterar(vm, "medias", "200,00");
        Opcao(vm, "adiantamento", 2);
        Alterar(vm, "valorAdiantamento", "1.500,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2025, 12, 1), parcelas.Pedido?.Competencia);
        Assert.Equal(3500m, parcelas.Pedido?.Salario);
        Assert.Equal(AdiantamentoDecimoTerceiro.ValorInformado, parcelas.Pedido?.Adiantamento);
        Assert.Equal(1500m, parcelas.Pedido?.ValorAdiantamento);
        Assert.Null(complemento.Pedido);
        Opcao(vm, "calculo", 1);
        Alterar(vm, "pagamento", "01/2026");
        Alterar(vm, "variaveisDezembro", "300,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 1, 1), complemento.Pedido?.Pagamento);
        Assert.Equal(300m, complemento.Pedido?.VariaveisDezembro);
    }

    [Fact]
    public async Task Ferias_preserva_periodos_e_bases_por_competencia()
    {
        var captura = new Captura<SimularFeriasRequest>();
        var vm = new FeriasMobileViewModel(captura, new PdfTeste(), NullLogger<FeriasMobileViewModel>.Instance);
        Alterar(vm, "competencia", "01/2026");
        Alterar(vm, "inicio", "28/01/2026");
        Alterar(vm, "salario", "4.000,00");
        Alterar(vm, "dias", "14");
        Alterar(vm, "segundo", "16");
        Alterar(vm, "baseSegundo", "2.000,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 1, 28), captura.Pedido?.InicioGozo);
        Assert.Equal(14, captura.Pedido?.DiasGozo);
        Assert.Equal(new[] { 16, 0 }, captura.Pedido?.OutrosPeriodos);
        Assert.Equal(2000m, captura.Pedido?.BaseForaFeriasMesSeguinte);
    }

    [Fact]
    public async Task Horas_de_relogio_nao_sao_lidas_como_horas_decimais()
    {
        var captura = new Captura<SimularHorasExtrasRequest>();
        var vm = new HorasExtrasMobileViewModel(captura, new PdfTeste(), NullLogger<HorasExtrasMobileViewModel>.Instance);
        Alterar(vm, "competencia", "03/2026");
        Alterar(vm, "salario", "3.500,00");
        Alterar(vm, "horas1", "10:30");
        Opcao(vm, "trabalho", 1);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(10.5m, captura.Pedido?.HorasFaixa1);
        Assert.Equal(25m, captura.Pedido?.PercentualNoturno);
        Assert.True(captura.Pedido?.Rural);
    }

    [Fact]
    public async Task Rescisao_encaminha_datas_motivo_e_valores_opcionais()
    {
        var captura = new Captura<SimularRescisaoRequest>();
        var vm = new RescisaoMobileViewModel(captura, new PdfTeste(), NullLogger<RescisaoMobileViewModel>.Instance);
        Alterar(vm, "admissao", "01/02/2020");
        Alterar(vm, "desligamento", "31/10/2026");
        Alterar(vm, "salario", "5.000,00");
        Alterar(vm, "saldoFgts", "15.000,00");
        Opcao(vm, "dataBase", 11);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2020, 2, 1), captura.Pedido?.Admissao);
        Assert.Equal(MotivoRescisao.DispensaSemJustaCausa, captura.Pedido?.Motivo);
        Assert.Equal(15000m, captura.Pedido?.SaldoFgts);
        Assert.Equal(11, captura.Pedido?.MesDataBase);
        Assert.Null(captura.Pedido?.DataPagamento);
    }

    [Fact]
    public async Task Historico_do_fgts_gera_meses_preserva_valores_e_valida_linhas()
    {
        var captura = new Captura<SimularRescisaoRequest>();
        var vm = new RescisaoMobileViewModel(captura, new PdfTeste(), NullLogger<RescisaoMobileViewModel>.Instance);
        Alterar(vm, "admissao", "01/01/2026");
        Alterar(vm, "desligamento", "15/03/2026");
        Alterar(vm, "salario", "3.000,00");
        vm.GerarDepositosCommand.Execute(null);
        Assert.Equal(["01/2026", "02/2026"], vm.DepositosFgts.Select(linha => linha.Competencia));
        vm.DepositosFgts[0].Valor = "250,00";
        vm.GerarDepositosCommand.Execute(null);
        Assert.Equal("250,00", vm.DepositosFgts[0].Valor);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(250m, captura.Pedido?.DepositosFgts?[0].Valor);
        captura.Limpar();
        vm.DepositosFgts[1].Competencia = "2026-02";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Contains("FGTS", vm.Mensagem);
    }

    [Fact]
    public async Task Campo_invalido_impede_execucao()
    {
        var captura = new Captura<SimularHorasExtrasRequest>();
        var vm = new HorasExtrasMobileViewModel(captura, new PdfTeste(), NullLogger<HorasExtrasMobileViewModel>.Instance);
        Alterar(vm, "competencia", "2026-03");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Contains("Competência", vm.Mensagem);
        Assert.False(vm.CompartilharPdfCommand.CanExecute(null));
    }

    [Fact]
    public async Task Pdf_recebe_o_mesmo_demonstrativo_e_entrada_editada_invalida_o_resultado()
    {
        var demonstrativo = new DemonstrativoDto("13º", "12/2026", [], [new("Integral", "12 avos", 3500m)], [], [], [], []);
        var parcelas = new Captura<SimularDecimoTerceiroRequest> { Resposta = demonstrativo };
        var pdf = new PdfTeste();
        var vm = new DecimoTerceiroMobileViewModel(parcelas, new Captura<SimularComplementoDecimoTerceiroRequest>(),
            pdf, NullLogger<DecimoTerceiroMobileViewModel>.Instance);
        Alterar(vm, "salario", "3.500,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Same(demonstrativo, vm.Demonstrativo);
        await vm.CompartilharPdfCommand.ExecuteAsync(null);
        Assert.Same(demonstrativo, pdf.Ultimo);
        Alterar(vm, "salario", "3.600,00");
        Assert.Null(vm.Demonstrativo);
        Assert.False(vm.CompartilharPdfCommand.CanExecute(null));
    }

    private static void Alterar(CalculadoraTrabalhistaViewModel vm, string chave, string valor) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave).Valor = valor;

    private static void Opcao(CalculadoraTrabalhistaViewModel vm, string chave, int indice) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave).Indice = indice;

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
        public DemonstrativoDto? Ultimo { get; private set; }
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken)
        {
            Ultimo = demonstrativo;
            return Task.CompletedTask;
        }
    }
}
