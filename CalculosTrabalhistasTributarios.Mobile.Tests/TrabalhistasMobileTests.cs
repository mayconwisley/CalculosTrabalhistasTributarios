using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
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
    public async Task Salario_familia_encaminha_remuneracao_integral_e_dias()
    {
        var captura = new Captura<SimularSalarioFamiliaRequest>();
        var vm = new SalarioFamiliaMobileViewModel(captura, new PdfTeste(), NullLogger<SalarioFamiliaMobileViewModel>.Instance);
        Alterar(vm, "competencia", "02/2026");
        Alterar(vm, "remuneracao", "2.350,50");
        Alterar(vm, "filhos", "2");
        Alterar(vm, "dias", "15");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 2, 1), captura.Pedido?.Competencia);
        Assert.Equal(2350.50m, captura.Pedido?.Remuneracao);
        Assert.Equal(2, captura.Pedido?.Filhos);
        Assert.Equal(15, captura.Pedido?.DiasTrabalhados);
    }

    [Fact]
    public async Task Adicionais_encaminha_grau_base_e_periculosidade()
    {
        var captura = new Captura<SimularAdicionaisRequest>();
        var vm = new AdicionaisMobileViewModel(captura, new PdfTeste(), NullLogger<AdicionaisMobileViewModel>.Instance);
        Assert.False(Assert.Single(vm.Campos, campo => campo.Chave == "valorBase").Visivel);
        Opcao(vm, "base", 2);
        Assert.True(Assert.Single(vm.Campos, campo => campo.Chave == "valorBase").Visivel);
        Opcao(vm, "grau", 3);
        Alterar(vm, "valorBase", "2.850,00");
        Assert.Single(vm.Campos, campo => campo.Chave == "periculosidade").Marcado = true;
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(GrauInsalubridade.Maximo, captura.Pedido?.Grau);
        Assert.Equal(BaseInsalubridade.ValorInformado, captura.Pedido?.Base);
        Assert.Equal(2850m, captura.Pedido?.ValorBaseInformado);
        Assert.True(captura.Pedido?.Periculosidade);
    }

    [Fact]
    public async Task Comissoes_alternam_calendario_e_dias_informados()
    {
        var captura = new Captura<SimularComissoesRequest>();
        var vm = new ComissoesMobileViewModel(captura, new PdfTeste(), NullLogger<ComissoesMobileViewModel>.Instance);
        Alterar(vm, "comissoes", "3.500,00");
        Alterar(vm, "feriados", "1");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(1, captura.Pedido?.Feriados);
        Assert.Null(captura.Pedido?.DiasUteis);
        Assert.Single(vm.Campos, campo => campo.Chave == "diasManuais").Marcado = true;
        Assert.False(Assert.Single(vm.Campos, campo => campo.Chave == "feriados").Visivel);
        Alterar(vm, "diasUteis", "22");
        Alterar(vm, "diasDescanso", "8");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(0, captura.Pedido?.Feriados);
        Assert.Equal(22, captura.Pedido?.DiasUteis);
        Assert.Equal(8, captura.Pedido?.DiasDescanso);
    }

    [Fact]
    public async Task Seguro_desemprego_alterna_carencia_e_domestico()
    {
        var captura = new Captura<SimularSeguroDesempregoRequest>();
        var vm = new SeguroDesempregoMobileViewModel(captura, new PdfTeste(), NullLogger<SeguroDesempregoMobileViewModel>.Instance);
        Alterar(vm, "dispensa", "15/08/2026");
        Alterar(vm, "ultimo", "3.500,00");
        Alterar(vm, "penultimo", "3.200,00");
        Alterar(vm, "meses18", "13");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 8, 15), captura.Pedido?.Dispensa);
        Assert.Equal([3500m, 3200m, 0m], captura.Pedido?.Salarios);
        Assert.Equal(13, captura.Pedido?.MesesNoPeriodoCarencia);
        Opcao(vm, "solicitacao", 1);
        Assert.True(Assert.Single(vm.Campos, campo => campo.Chave == "meses12").Visivel);
        Assert.False(Assert.Single(vm.Campos, campo => campo.Chave == "meses18").Visivel);
        Alterar(vm, "meses12", "10");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(SolicitacaoSeguroDesemprego.Segunda, captura.Pedido?.Solicitacao);
        Assert.Equal(10, captura.Pedido?.MesesNoPeriodoCarencia);
        Opcao(vm, "vinculo", 1);
        Assert.False(Assert.Single(vm.Campos, campo => campo.Chave == "ultimo").Visivel);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.True(captura.Pedido?.Domestico);
        Assert.Null(captura.Pedido?.MesesNoPeriodoCarencia);
    }

    [Fact]
    public async Task Custo_do_funcionario_encaminha_regime_e_beneficios_detalhados()
    {
        var demonstrativo = new DemonstrativoDto("Custo", "Mês", [], [new("Salário", "", 3500m)], [], [], [], []);
        var captura = new Captura<SimularCustoFuncionarioRequest> { Resposta = demonstrativo };
        var vm = new CustoFuncionarioMobileViewModel(captura, new PdfTeste(), NullLogger<CustoFuncionarioMobileViewModel>.Instance);
        Alterar(vm, "salario", "3.500,00");
        Opcao(vm, "regime", 2);
        Assert.False(Assert.Single(vm.Campos, campo => campo.Chave == "terceiros").Visivel);
        Assert.True(Assert.Single(vm.Campos, campo => campo.Chave == "fap").Visivel);
        vm.AdicionarBeneficioCommand?.Execute(null);
        var linha = Assert.Single(vm.BeneficiosDetalhados);
        linha.Descricao = "Vale-alimentação";
        linha.Custo = "500,00";
        linha.Participacao = "80,00";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(RegimeTributario.SimplesNacionalAnexoIV, captura.Pedido?.Regime);
        Assert.Equal(2m, captura.Pedido?.Rat);
        Assert.Equal(0m, captura.Pedido?.Terceiros);
        Assert.True(captura.Pedido?.IncluirProvisoes);
        var beneficio = Assert.Single(captura.Pedido!.BeneficiosDetalhados!);
        Assert.Equal("Vale-alimentação", beneficio.Descricao);
        Assert.Equal(420m, beneficio.CustoLiquido);
        Assert.Same(demonstrativo, vm.Demonstrativo);
        linha.Custo = "550,00";
        Assert.Null(vm.Demonstrativo);
        vm.RemoverBeneficioCommand?.Execute(linha);
        Assert.Empty(vm.BeneficiosDetalhados);
    }

    [Fact]
    public async Task Pro_labore_alterna_iss_e_encaminha_regime()
    {
        var captura = new Captura<SimularProLaboreRequest>();
        var vm = new ProLaboreMobileViewModel(captura, new PdfTeste(), NullLogger<ProLaboreMobileViewModel>.Instance);
        Assert.False(Assert.Single(vm.Campos, campo => campo.Chave == "iss").Visivel);
        Opcao(vm, "tipo", 1);
        Assert.True(Assert.Single(vm.Campos, campo => campo.Chave == "iss").Visivel);
        Alterar(vm, "valor", "5.000,00");
        Alterar(vm, "iss", "3");
        Opcao(vm, "regime", 1);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(TipoContribuinteIndividual.Autonomo, captura.Pedido?.Tipo);
        Assert.Equal(5000m, captura.Pedido?.Valor);
        Assert.Equal(3m, captura.Pedido?.AliquotaIss);
        Assert.Equal(RegimeTributario.SimplesNacional, captura.Pedido?.Regime);
    }

    [Fact]
    public async Task Plr_encaminha_valores_anteriores_e_pensao()
    {
        var captura = new Captura<SimularPlrRequest>();
        var vm = new PlrMobileViewModel(captura, new PdfTeste(), NullLogger<PlrMobileViewModel>.Instance);
        Alterar(vm, "competencia", "09/2026");
        Alterar(vm, "valor", "10.000,00");
        Alterar(vm, "anterior", "4.000,00");
        Alterar(vm, "impostoAnterior", "150,00");
        Opcao(vm, "pensaoBase", 2);
        Alterar(vm, "pensaoPercentual", "20");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 9, 1), captura.Pedido?.Competencia);
        Assert.Equal(4000m, captura.Pedido?.PlrAnterior);
        Assert.Equal(150m, captura.Pedido?.ImpostoRetidoAnterior);
        Assert.Equal(BasePensao.RendimentosBrutos, captura.Pedido?.Pensao?.Base);
        Assert.Equal(20m, captura.Pedido?.Pensao?.Percentual);
    }

    [Fact]
    public async Task Clt_pj_encaminha_equivalencia_e_tributacao()
    {
        var captura = new Captura<SimularCltPjRequest>();
        var vm = new CltPjMobileViewModel(captura, new PdfTeste(), NullLogger<CltPjMobileViewModel>.Instance);
        Alterar(vm, "salario", "6.000,00");
        Alterar(vm, "beneficios", "800,00");
        Opcao(vm, "regime", 2);
        Opcao(vm, "tributacao", 2);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(6000m, captura.Pedido?.SalarioClt);
        Assert.Equal(800m, captura.Pedido?.BeneficiosClt);
        Assert.Equal(0m, captura.Pedido?.ValorPj);
        Assert.Equal(RegimeTributario.SimplesNacionalAnexoIV, captura.Pedido?.RegimeEmpresa);
        Assert.Equal(TributacaoPj.AnexoV, captura.Pedido?.Tributacao);
    }

    [Fact]
    public async Task Holerite_encaminha_horas_comissoes_pensao_e_descontos()
    {
        var captura = new Captura<SimularHoleriteRequest>();
        var vm = new HoleriteMobileViewModel(captura, new PdfTeste(), NullLogger<HoleriteMobileViewModel>.Instance);
        Assert.Contains(vm.Campos, campo => campo.EhSecao && !campo.EhEntrada);
        Assert.False(Assert.Single(vm.Campos, campo => campo.Chave == "diasUteisComissoes").Visivel);
        Assert.False(Assert.Single(vm.Campos, campo => campo.Chave == "pensaoPercentual").Visivel);
        Alterar(vm, "competencia", "03/2026");
        Alterar(vm, "salario", "3.200,00");
        Opcao(vm, "insalubridade", 2);
        Alterar(vm, "horas1", "10:30");
        Alterar(vm, "horas2", "4");
        Opcao(vm, "trabalho", 1);
        Assert.Equal("25", Assert.Single(vm.Campos, campo => campo.Chave == "percentualNoturno").Valor);
        Alterar(vm, "horasNoturnas", "20:00");
        Alterar(vm, "comissoes", "1.500,00");
        Assert.Single(vm.Campos, campo => campo.Chave == "diasComissoesManuais").Marcado = true;
        Assert.True(Assert.Single(vm.Campos, campo => campo.Chave == "diasUteisComissoes").Visivel);
        Alterar(vm, "diasUteisComissoes", "24");
        Alterar(vm, "diasDescansoComissoes", "7");
        Alterar(vm, "faltas", "1");
        Alterar(vm, "atrasos", "1:15");
        Opcao(vm, "pensaoBase", 1);
        Assert.True(Assert.Single(vm.Campos, campo => campo.Chave == "pensaoPercentual").Visivel);
        Alterar(vm, "pensaoPercentual", "30");
        Alterar(vm, "valeTransporte", "250,00");
        Alterar(vm, "planoSaude", "180,00");
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = captura.Pedido!;
        Assert.Equal(new DateOnly(2026, 3, 1), pedido.Competencia);
        Assert.Equal(3200m, pedido.Salario);
        Assert.Equal(GrauInsalubridade.Medio, pedido.Insalubridade);
        Assert.Equal(10.5m, pedido.HorasFaixa1);
        Assert.Equal(4m, pedido.HorasFaixa2);
        Assert.True(pedido.Rural);
        Assert.Equal(25m, pedido.PercentualNoturno);
        Assert.Equal(20m, pedido.HorasNoturnas);
        Assert.Equal(1500m, pedido.Comissoes);
        Assert.Equal(24, pedido.DiasUteisComissoes);
        Assert.Equal(7, pedido.DiasDescansoComissoes);
        Assert.Equal(1, pedido.Faltas);
        Assert.Equal(1.25m, pedido.HorasAtraso);
        Assert.Equal(BasePensao.RendimentosLiquidos, pedido.Pensao?.Base);
        Assert.Equal(30m, pedido.Pensao?.Percentual);
        Assert.Equal(250m, pedido.CustoValeTransporte);
        Assert.Equal(180m, pedido.PlanoSaude);
        Assert.Null(pedido.QuitacaoBancoHoras);
        Assert.Single(vm.Campos, campo => campo.Chave == "diasComissoesManuais").Marcado = false;
        Opcao(vm, "pensaoBase", 0);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido?.DiasUteisComissoes);
        Assert.Null(captura.Pedido?.Pensao);
        Alterar(vm, "horas1", "10:75");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Contains("Horas extras: faixa 1", vm.Mensagem);
    }

    [Fact]
    public async Task Banco_de_horas_encaminha_ciclo_e_lancamentos_em_minutos()
    {
        var captura = new Captura<SimularBancoHorasRequest>();
        var vm = new BancoHorasMobileViewModel(captura, new NavegacaoTeste(), new PdfTeste(), NullLogger<BancoHorasMobileViewModel>.Instance);
        Opcao(vm, "regime", 1);
        Opcao(vm, "situacao", 1);
        Alterar(vm, "inicio", "01/03/2026");
        Alterar(vm, "fim", "31/03/2026");
        Alterar(vm, "salario", "2.200,00");
        var credito = Assert.Single(vm.LancamentosBancoHoras);
        credito.Data = "02/03/2026";
        credito.Horas = "1:30";
        credito.Adicional = "70";
        credito.Descricao = "Fechamento do estoque";
        vm.AdicionarLancamentoCommand.Execute(null);
        var folga = vm.LancamentosBancoHoras[1];
        Assert.Equal("31/03/2026", folga.Data);
        folga.Tipo = 1;
        folga.Horas = "0:45";
        folga.Adicional = "999999";
        Assert.False(folga.EhCredito);
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = captura.Pedido!;
        Assert.Equal(new DateOnly(2026, 3, 1), pedido.Inicio);
        Assert.Equal(new DateOnly(2026, 3, 31), pedido.Fim);
        Assert.Equal(RegimeBancoHoras.AcordoIndividualEscrito, pedido.Regime);
        Assert.Equal(SituacaoBancoHoras.Fechamento, pedido.Situacao);
        Assert.Equal(2200m, pedido.Salario);
        Assert.Equal(220m, pedido.Divisor);
        Assert.Equal(50m, pedido.Adicional);
        Assert.Collection(pedido.Lancamentos,
            item => Assert.Equal(new LancamentoBancoHoras(new DateOnly(2026, 3, 2), TipoLancamentoBancoHoras.Credito, 90, "Fechamento do estoque", 70m), item),
            item => Assert.Equal(new LancamentoBancoHoras(new DateOnly(2026, 3, 31), TipoLancamentoBancoHoras.Compensacao, 45, "", null), item));

        captura.Limpar();
        folga.Horas = "1,5";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Equal("Lançamento 2: informe as horas de relógio como 1:30, até 10:00.", vm.Mensagem);
        vm.RemoverLancamentoCommand.Execute(folga);
        Assert.Single(vm.LancamentosBancoHoras);
    }

    [Fact]
    public async Task Banco_de_horas_transfere_quitacao_somente_para_o_destino_da_situacao()
    {
        var quitacao = QuitacaoTeste(SituacaoBancoHoras.Fechamento);
        var demonstrativo = new DemonstrativoDto("Banco", "Ciclo", [], [], [], [], [], [], QuitacaoBancoHoras: quitacao);
        var navegacao = new NavegacaoTeste();
        var vm = new BancoHorasMobileViewModel(new Captura<SimularBancoHorasRequest> { Resposta = demonstrativo }, navegacao,
            new PdfTeste(), NullLogger<BancoHorasMobileViewModel>.Instance);
        Assert.Single(vm.LancamentosBancoHoras).Horas = "2:00";
        Assert.False(vm.PodeUsarNoHolerite);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.True(vm.PodeUsarNoHolerite);
        Assert.False(vm.PodeUsarNaRescisao);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.UsarNaRescisaoCommand).ExecuteAsync(null);
        Assert.Null(navegacao.Destino);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.UsarNoHoleriteCommand).ExecuteAsync(null);
        Assert.Equal(CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio.TipoCalculadoraMobile.Holerite, navegacao.Destino);
        Assert.Same(quitacao, navegacao.Quitacao);
        Assert.Single(vm.LancamentosBancoHoras).Horas = "1:00";
        Assert.False(vm.PodeUsarNoHolerite);
    }

    [Fact]
    public async Task Holerite_recebe_quitacao_e_exige_confirmacao()
    {
        var captura = new Captura<SimularHoleriteRequest>();
        var vm = new HoleriteMobileViewModel(captura, new PdfTeste(), NullLogger<HoleriteMobileViewModel>.Instance);
        var quitacao = QuitacaoTeste(SituacaoBancoHoras.Fechamento);
        vm.ReceberQuitacao(quitacao);
        Assert.True(vm.TemQuitacaoBancoHoras);
        Assert.Contains("Total transferido: R$ 30,00", vm.QuitacaoBancoHorasTexto);
        Assert.Equal("03/2026", Assert.Single(vm.Campos, campo => campo.Chave == "competencia").Valor);
        Assert.Equal("2.200,00", Assert.Single(vm.Campos, campo => campo.Chave == "salario").Valor);
        Assert.Equal("220,00", Assert.Single(vm.Campos, campo => campo.Chave == "divisor").Valor);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Contains("marque a confirmação", vm.Mensagem);
        vm.QuitacaoConfirmada = true;
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Same(quitacao, captura.Pedido?.QuitacaoBancoHoras);
        Assert.Equal(new DateOnly(2026, 3, 1), captura.Pedido?.Competencia);
        vm.DescartarQuitacaoCommand.Execute(null);
        Assert.False(vm.TemQuitacaoBancoHoras);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido?.QuitacaoBancoHoras);
    }

    [Fact]
    public async Task Rescisao_recebe_quitacao_com_data_de_desligamento()
    {
        var captura = new Captura<SimularRescisaoRequest>();
        var vm = new RescisaoMobileViewModel(captura, new PdfTeste(), NullLogger<RescisaoMobileViewModel>.Instance);
        var quitacao = QuitacaoTeste(SituacaoBancoHoras.Rescisao);
        vm.ReceberQuitacao(quitacao);
        vm.QuitacaoConfirmada = true;
        Alterar(vm, "admissao", "01/02/2024");
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 3, 31), captura.Pedido?.Desligamento);
        Assert.Equal(2200m, captura.Pedido?.Salario);
        Assert.Same(quitacao, captura.Pedido?.QuitacaoBancoHoras);
    }

    // 2:00 a 50% sobre 2.200,00 ÷ 220: 2.200 × 1,5 × 120 ÷ 13.200 = 30,00.
    private static QuitacaoBancoHoras QuitacaoTeste(SituacaoBancoHoras situacao) =>
        new(new DateOnly(2026, 3, 31), situacao, 2200m, 220m, [new ParcelaQuitacaoBancoHoras(50m, 120, 30m)]);

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

    private sealed class NavegacaoTeste : INavegacaoCalculadoras
    {
        public CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio.TipoCalculadoraMobile? Destino { get; private set; }
        public QuitacaoBancoHoras? Quitacao { get; private set; }
        public Task AbrirAsync(CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio.TipoCalculadoraMobile calculadora)
        {
            Destino = calculadora;
            return Task.CompletedTask;
        }
        public Task AbrirAsync(CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio.TipoCalculadoraMobile calculadora, QuitacaoBancoHoras quitacao)
        {
            Destino = calculadora;
            Quitacao = quitacao;
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(TipoCampoMobile.Numero, true)]
    [InlineData(TipoCampoMobile.Inteiro, true)]
    [InlineData(TipoCampoMobile.Data, false)]
    [InlineData(TipoCampoMobile.Competencia, false)]
    [InlineData(TipoCampoMobile.Horas, false)]
    [InlineData(TipoCampoMobile.Texto, false)]
    public void Teclado_numerico_fica_so_nos_campos_sem_barra_dois_pontos_ou_letras(TipoCampoMobile tipo, bool numerico)
    {
        var campo = new CampoMobile("campo", "Campo", "", tipo);
        Assert.True(campo.EhTexto);
        Assert.Equal(numerico, campo.EhNumerico);
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
