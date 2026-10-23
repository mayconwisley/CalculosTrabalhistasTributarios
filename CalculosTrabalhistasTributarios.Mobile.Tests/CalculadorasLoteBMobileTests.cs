using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CalculadorasLoteBMobileTests
{
    [Fact]
    public async Task Emprestimo_pessoal_le_iof_informado_somente_quando_selecionado()
    {
        var captura = new Captura<SimularEmprestimoPessoalRequest>();
        var vm = new EmprestimoPessoalMobileViewModel(captura, new PdfTeste(), NullLogger<EmprestimoPessoalMobileViewModel>.Instance);
        Assert.False(Campo(vm, "iof").Visivel);
        Alterar(vm, "valor", "10.000,00");
        Alterar(vm, "parcelas", "12");
        Alterar(vm, "juros", "2,5");
        Alterar(vm, "data", "15/03/2026");
        Alterar(vm, "iof", "150,00");
        Alterar(vm, "seguro", "300,00");
        Alterar(vm, "custosLiberacao", "50,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        var entrada = captura.Pedido?.Emprestimo;
        Assert.Equal(10000m, entrada?.ValorSolicitado);
        Assert.Equal(12, entrada?.NumeroParcelas);
        Assert.Equal(2.5m, entrada?.JurosMensais);
        Assert.Equal(new DateOnly(2026, 3, 15), entrada?.DataLiberacao);
        Assert.True(entrada?.IofAutomatico);
        Assert.Equal(0m, entrada?.IofInformado);
        Assert.True(entrada?.FinanciarIof);
        Assert.Equal(300m, entrada?.SeguroFinanciado);
        Assert.Equal(50m, entrada?.CustosNaLiberacao);

        Opcao(vm, "modoIof", 1);
        Opcao(vm, "cobrancaIof", 1);
        Assert.True(Campo(vm, "iof").Visivel);
        await vm.CalcularCommand.ExecuteAsync(null);
        entrada = captura.Pedido?.Emprestimo;
        Assert.False(entrada?.IofAutomatico);
        Assert.Equal(150m, entrada?.IofInformado);
        Assert.False(entrada?.FinanciarIof);
    }

    [Fact]
    public async Task Estagio_encaminha_bolsa_datas_e_recesso()
    {
        var captura = new Captura<SimularEstagioRequest>();
        var vm = new EstagioMobileViewModel(captura, new PdfTeste(), NullLogger<EstagioMobileViewModel>.Instance);
        Alterar(vm, "competencia", "06/2026");
        Alterar(vm, "bolsa", "1.500,00");
        Alterar(vm, "auxilio", "200,00");
        Alterar(vm, "diasNoMes", "15");
        Alterar(vm, "inicio", "01/07/2025");
        Alterar(vm, "fim", "30/06/2026");
        Alterar(vm, "gozados", "10,5");
        Alterar(vm, "dependentes", "1");
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = captura.Pedido;
        Assert.Equal(new DateOnly(2026, 6, 1), pedido?.Competencia);
        Assert.Equal(1500m, pedido?.Bolsa);
        Assert.Equal(200m, pedido?.AuxilioTransporte);
        Assert.Equal(new DateOnly(2025, 7, 1), pedido?.Inicio);
        Assert.Equal(new DateOnly(2026, 6, 30), pedido?.Fim);
        Assert.Equal(10.5m, pedido?.DiasRecessoGozados);
        Assert.Equal(1, pedido?.Dependentes);
        Assert.Equal(15, pedido?.DiasNoMes);
    }

    [Fact]
    public async Task Ganho_de_capital_ignora_isencoes_ocultas_pelo_tipo_de_bem()
    {
        var captura = new Captura<SimularGanhoCapitalRequest>();
        var vm = new GanhoCapitalMobileViewModel(captura, new PdfTeste(), NullLogger<GanhoCapitalMobileViewModel>.Instance);
        Alterar(vm, "aquisicao", "10/05/2010");
        Alterar(vm, "venda", "20/08/2026");
        Alterar(vm, "custo", "200.000,00");
        Alterar(vm, "valorVenda", "500.000,00");
        Alterar(vm, "despesas", "25.000,00");
        Campo(vm, "unico").Marcado = true;
        Alterar(vm, "reinvestido", "100.000,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = captura.Pedido;
        Assert.Equal(BemAlienado.ImovelResidencial, pedido?.Bem);
        Assert.Equal(new DateOnly(2010, 5, 10), pedido?.Aquisicao);
        Assert.Equal(new DateOnly(2026, 8, 20), pedido?.Alienacao);
        Assert.Equal(200000m, pedido?.Custo);
        Assert.Equal(500000m, pedido?.Venda);
        Assert.Equal(25000m, pedido?.DespesasVenda);
        Assert.True(pedido?.UnicoImovel);
        Assert.Equal(100000m, pedido?.Reinvestido);

        Opcao(vm, "bem", 1);
        Assert.True(Campo(vm, "unico").Visivel);
        Assert.False(Campo(vm, "reinvestido").Visivel);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(BemAlienado.OutroImovel, captura.Pedido?.Bem);
        Assert.True(captura.Pedido?.UnicoImovel);
        Assert.Equal(0m, captura.Pedido?.Reinvestido);

        Opcao(vm, "bem", 2);
        Assert.False(Campo(vm, "unico").Visivel);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(BemAlienado.OutroBem, captura.Pedido?.Bem);
        Assert.False(captura.Pedido?.UnicoImovel);
        Assert.Equal(0m, captura.Pedido?.Reinvestido);
    }

    [Fact]
    public async Task Intermitente_le_horas_de_relogio()
    {
        var captura = new Captura<SimularIntermitenteRequest>();
        var vm = new IntermitenteMobileViewModel(captura, new PdfTeste(), NullLogger<IntermitenteMobileViewModel>.Instance);
        Alterar(vm, "competencia", "04/2026");
        Alterar(vm, "valorHora", "12,50");
        Alterar(vm, "horas", "40:30");
        Alterar(vm, "dias", "6");
        Alterar(vm, "descansos", "2");
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = captura.Pedido;
        Assert.Equal(new DateOnly(2026, 4, 1), pedido?.Competencia);
        Assert.Equal(12.5m, pedido?.ValorHora);
        Assert.Equal(40.5m, pedido?.Horas);
        Assert.Equal(6, pedido?.DiasTrabalhados);
        Assert.Equal(2, pedido?.Descansos);
    }

    [Fact]
    public async Task Irpf_anual_converte_tipo_de_empresa_pela_aliquota_nominal()
    {
        var captura = new Captura<SimularIrpfAnualRequest>();
        var vm = new IrpfAnualMobileViewModel(captura, new PdfTeste(), NullLogger<IrpfAnualMobileViewModel>.Instance);
        Alterar(vm, "ano", "2026");
        Alterar(vm, "tributaveis", "120.000,00");
        Alterar(vm, "aposentadoria", "30.000,00");
        Alterar(vm, "mesesSessentaECinco", "6");
        Alterar(vm, "previdencia", "9.000,00");
        Alterar(vm, "dependentes", "2");
        Alterar(vm, "medicas", "5.000,00");
        Alterar(vm, "instrucao", "3.000,00");
        Alterar(vm, "pgbl", "4.000,00");
        Alterar(vm, "pensao", "1.000,00");
        Alterar(vm, "impostoPago", "15.000,00");
        Alterar(vm, "dividendos", "800.000,00");
        Alterar(vm, "irrfDividendos", "20.000,00");
        Alterar(vm, "outros", "10.000,00");
        Alterar(vm, "impostoExclusivo", "1.500,00");
        Alterar(vm, "aliquotaEmpresa", "25,5");
        Opcao(vm, "tipoEmpresa", 2);
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = captura.Pedido;
        Assert.Equal(2026, pedido?.Ano);
        Assert.Equal(120000m, pedido?.RendimentosTributaveis);
        Assert.Equal(30000m, pedido?.AposentadoriaSessentaECinco);
        Assert.Equal(6, pedido?.MesesSessentaECinco);
        Assert.Equal(9000m, pedido?.PrevidenciaOficial);
        Assert.Equal(2, pedido?.Dependentes);
        Assert.Equal(5000m, pedido?.DespesasMedicas);
        Assert.Equal(3000m, pedido?.Instrucao);
        Assert.Equal(4000m, pedido?.PrevidenciaComplementar);
        Assert.Equal(1000m, pedido?.PensaoAlimenticia);
        Assert.Equal(15000m, pedido?.ImpostoPago);
        Assert.Equal(800000m, pedido?.Dividendos);
        Assert.Equal(20000m, pedido?.IrrfDividendos);
        Assert.Equal(10000m, pedido?.OutrosRendimentos);
        Assert.Equal(1500m, pedido?.ImpostoExclusivo);
        Assert.Equal(25.5m, pedido?.AliquotaEfetivaEmpresa);
        Assert.Equal(AliquotaNominalEmpresa.Banco, pedido?.AliquotaNominal);

        Opcao(vm, "tipoEmpresa", 1);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(AliquotaNominalEmpresa.Financeira, captura.Pedido?.AliquotaNominal);
    }

    [Fact]
    public async Task Revisao_de_pensao_monta_as_duas_regras_conforme_a_base()
    {
        var captura = new Captura<SimularRevisaoPensaoRequest>();
        var vm = new RevisaoPensaoMobileViewModel(captura, new PdfTeste(), NullLogger<RevisaoPensaoMobileViewModel>.Instance);
        Assert.True(Campo(vm, "atualPercentual").Visivel);
        Assert.False(Campo(vm, "atualValor").Visivel);
        Alterar(vm, "competencia", "05/2026");
        Alterar(vm, "valorBruto", "8.000,00");
        Alterar(vm, "dependentes", "1");
        Opcao(vm, "atualBase", 2);
        Alterar(vm, "atualPercentual", "150");
        Opcao(vm, "propostaBase", 3);
        Assert.False(Campo(vm, "propostaPercentual").Visivel);
        Assert.True(Campo(vm, "propostaValor").Visivel);
        Alterar(vm, "propostaValor", "1.800,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = captura.Pedido;
        Assert.Equal(new DateOnly(2026, 5, 1), pedido?.Competencia);
        Assert.Equal(8000m, pedido?.ValorBruto);
        Assert.Equal(1, pedido?.Dependentes);
        Assert.Equal(new RegraPensao(BasePensao.SalarioMinimo, 150m, 0m), pedido?.Atual);
        Assert.Equal(new RegraPensao(BasePensao.ValorFixo, 0m, 1800m), pedido?.Proposta);

        Opcao(vm, "atualBase", 0);
        Alterar(vm, "atualPercentual", "30");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new RegraPensao(BasePensao.RendimentosLiquidos, 30m, 0m), captura.Pedido?.Atual);
    }

    [Fact]
    public async Task Salario_pelo_liquido_encaminha_liquido_desejado()
    {
        var captura = new Captura<SimularSalarioPeloLiquidoRequest>();
        var vm = new SalarioPeloLiquidoMobileViewModel(captura, new PdfTeste(), NullLogger<SalarioPeloLiquidoMobileViewModel>.Instance);
        Alterar(vm, "competencia", "02/2026");
        Alterar(vm, "liquido", "4.250,75");
        Alterar(vm, "dependentes", "3");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 2, 1), captura.Pedido?.Competencia);
        Assert.Equal(4250.75m, captura.Pedido?.LiquidoDesejado);
        Assert.Equal(3, captura.Pedido?.Dependentes);
    }

    [Fact]
    public async Task Tributo_em_atraso_converte_guia_e_datas()
    {
        var captura = new Captura<SimularTributoAtrasoRequest>();
        var vm = new TributoAtrasoMobileViewModel(captura, new PdfTeste(), NullLogger<TributoAtrasoMobileViewModel>.Instance);
        Opcao(vm, "guia", 3);
        Alterar(vm, "principal", "1.234,56");
        Alterar(vm, "vencimento", "20/01/2026");
        Alterar(vm, "pagamento", "10/03/2026");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(GuiaDeRecolhimento.Gps, captura.Pedido?.Guia);
        Assert.Equal(1234.56m, captura.Pedido?.Principal);
        Assert.Equal(new DateOnly(2026, 1, 20), captura.Pedido?.Vencimento);
        Assert.Equal(new DateOnly(2026, 3, 10), captura.Pedido?.Pagamento);
    }

    [Fact]
    public async Task Data_invalida_vira_mensagem_sem_chamar_o_caso_de_uso()
    {
        var captura = new Captura<SimularTributoAtrasoRequest>();
        var vm = new TributoAtrasoMobileViewModel(captura, new PdfTeste(), NullLogger<TributoAtrasoMobileViewModel>.Instance);
        Alterar(vm, "vencimento", "31/02/2026");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Contains("Vencimento", vm.Mensagem ?? string.Empty);
    }

    private static CampoMobile Campo(CalculadoraTrabalhistaViewModel vm, string chave) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave);

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
