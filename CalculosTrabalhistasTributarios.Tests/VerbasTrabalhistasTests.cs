using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Tests.Referencia;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class RescisaoTests
{
    private static async Task<DemonstrativoDto> CalcularAsync(SimularRescisaoRequest request) =>
        await new SimularRescisaoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(request, default).Sucesso();

    private static decimal Valor(IEnumerable<VerbaDto> verbas, string descricao) => verbas.Single(verba => verba.Descricao.StartsWith(descricao)).Valor;

    [Fact]
    public async Task Fgts_do_mes_abate_a_primeira_parcela_do_13o_ja_depositada()
    {
        var resultado = await CalcularAsync(new SimularRescisaoRequest(new DateOnly(2020, 1, 6), new DateOnly(2026, 12, 15), MotivoRescisao.DispensaSemJustaCausa,
            CumprimentoAvisoPrevio.Indenizado, 4000m, 0m, 0, 0, 20_000m, 2000m, 0));
        // (2.000 de saldo + 6.400 de aviso + 4.333,33 de 13º − 2.000 da 1ª parcela) × 8%.
        Assert.Equal(858.67m, Valor(resultado.Informativos, "Depósito do FGTS"));
        Assert.Equal(8343.47m, Valor(resultado.Informativos, "Multa rescisória"));
        Assert.Equal(29_202.14m, Valor(resultado.Informativos, "FGTS disponível"));
    }

    [Fact]
    public async Task Com_dois_periodos_vencidos_o_mais_antigo_sai_em_dobro()
    {
        var resultado = await CalcularAsync(new SimularRescisaoRequest(new DateOnly(2023, 5, 10), new DateOnly(2026, 10, 20), MotivoRescisao.DispensaSemJustaCausa,
            CumprimentoAvisoPrevio.Indenizado, 3000m, 0m, 2, 0, 0m, 0m, 0));
        Assert.Equal(3000m, resultado.Proventos.Single(verba => verba.Descricao == "Férias vencidas").Valor);
        Assert.Equal(6000m, Valor(resultado.Proventos, "Férias vencidas em dobro"));
        Assert.Equal(3000m, Valor(resultado.Proventos, "1/3 sobre férias vencidas"));
    }

    [Theory]
    [InlineData("2026-02-02", "2026-02-28", 29)]
    [InlineData("2026-01-02", "2026-01-31", 29)]
    [InlineData("2026-03-01", "2026-03-31", 30)]
    [InlineData("2026-01-31", "2026-01-31", 1)]
    [InlineData("2025-08-10", "2026-02-28", 30)]
    [InlineData("2025-08-10", "2026-03-15", 15)]
    public async Task Saldo_de_salario_usa_o_mes_comercial_de_30_dias(string admissao, string desligamento, int dias)
    {
        var resultado = await CalcularAsync(new SimularRescisaoRequest(DateOnly.Parse(admissao), DateOnly.Parse(desligamento), MotivoRescisao.PedidoDeDemissao,
            CumprimentoAvisoPrevio.TrabalhadoOuDispensado, 3000m, 0m, 0, 0, 0m, 0m, 0));
        Assert.Equal(100m * dias, Valor(resultado.Proventos, "Saldo de salário"));
    }

    [Fact]
    public async Task Saldo_do_fgts_estimado_nao_conta_duas_vezes_o_mes_do_desligamento()
    {
        var resultado = await CalcularAsync(new SimularRescisaoRequest(new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 29), MotivoRescisao.TerminoDeContratoPorPrazo,
            CumprimentoAvisoPrevio.TrabalhadoOuDispensado, 2500m, 0m, 0, 0, 0m, 0m, 0));
        Assert.Equal(400m, Valor(resultado.Informativos, "Saldo do FGTS"));
        Assert.Equal(643.33m, Valor(resultado.Informativos, "FGTS disponível"));
    }

    [Fact]
    public async Task Irrf_usa_a_tabela_do_mes_do_pagamento()
    {
        SimularRescisaoRequest Pago(DateOnly? pagamento) => new(new DateOnly(2020, 1, 6), new DateOnly(2025, 12, 26), MotivoRescisao.DispensaSemJustaCausa,
            CumprimentoAvisoPrevio.TrabalhadoOuDispensado, 6000m, 0m, 0, 0, 0m, 0m, 0, null, pagamento);
        var dezembro = await CalcularAsync(Pago(null));
        var janeiro = await CalcularAsync(Pago(new DateOnly(2026, 1, 1)));
        Assert.Equal(357.89m, Valor(dezembro.Descontos, "IRRF sobre o saldo"));
        Assert.Equal(562.64m, Valor(dezembro.Descontos, "IRRF sobre o 13º"));
        // Em 01/2026 vale a redução mensal da Lei 15.270/2025; o INSS continua o da competência do desligamento.
        Assert.Equal(71.62m, Valor(janeiro.Descontos, "IRRF sobre o saldo"));
        Assert.Equal(382.89m, Valor(janeiro.Descontos, "IRRF sobre o 13º"));
        Assert.Equal(Valor(dezembro.Descontos, "INSS sobre o saldo"), Valor(janeiro.Descontos, "INSS sobre o saldo"));
    }

    [Fact]
    public async Task Projecao_do_aviso_gera_avos_e_a_memoria_fecha_com_o_valor()
    {
        var resultado = await CalcularAsync(new SimularRescisaoRequest(new DateOnly(2020, 1, 6), new DateOnly(2026, 11, 20), MotivoRescisao.PedidoDeDemissao,
            CumprimentoAvisoPrevio.TrabalhadoOuDispensado, 4000m, 0m, 0, 0, 0m, 0m, 0));
        Assert.Equal(3666.67m, Valor(resultado.Proventos, "13º salário proporcional"));
        Assert.Contains(resultado.Memoria.SelectMany(grupo => grupo.Formulas), formula => Texto.Normalizar(formula.Formula).EndsWith("11 avos = R$ 3.666,67"));
    }
}

public class EstabilidadeTests
{
    [Fact]
    public void Indenizacao_e_por_meses_cheios_e_dias_que_sobram()
    {
        var resultado = new SimularEstabilidadeUseCase().Executar(new SimularEstabilidadeRequest(3000m, 30, new DateOnly(2026, 3, 1), new DateOnly(2027, 3, 1), 0m)).Sucesso();
        Assert.Equal((12, 0, 12, 12), (resultado.Meses, resultado.DiasAlemDosMeses, resultado.AvosDecimoTerceiro, resultado.AvosFerias));
        Assert.Equal(36_000m, resultado.Indenizacao);
        Assert.Equal(47_368m, resultado.Total);
    }

    [Theory]
    [InlineData("2026-01-10", "2026-02-20", 1, 10, 2)]
    [InlineData("2026-01-15", "2026-02-20", 1, 5, 1)]
    [InlineData("2026-10-03", "2027-04-18", 6, 15, 7)]
    public void Avos_de_13o_somam_os_dias_do_mes_da_demissao(string demissao, string fim, int meses, int dias, int avos13)
    {
        var resultado = new SimularEstabilidadeUseCase().Executar(new SimularEstabilidadeRequest(3000m, 30, DateOnly.Parse(demissao), DateOnly.Parse(fim), 0m)).Sucesso();
        Assert.Equal((meses, dias, avos13), (resultado.Meses, resultado.DiasAlemDosMeses, resultado.AvosDecimoTerceiro));
        Assert.Equal(3000m * meses + 100m * dias, resultado.Indenizacao);
    }
}

public class HorasExtrasTests
{
    private static async Task<Result<DemonstrativoDto>> ResultadoAsync(SimularHorasExtrasRequest request) =>
        await new SimularHorasExtrasUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(request, default);

    private static Task<DemonstrativoDto> CalcularAsync(SimularHorasExtrasRequest request) => ResultadoAsync(request).Sucesso();

    [Fact]
    public async Task Hora_extra_noturna_leva_o_adicional_noturno_na_base()
    {
        // 7 horas de relógio = 8 horas noturnas; R$ 10,00 × 1,2 × 1,5 × 8 = R$ 144,00 (OJ 97 da SDI-1).
        var resultado = await CalcularAsync(new SimularHorasExtrasRequest(new DateOnly(2026, 10, 1), 2200m, 220m, 0m, 50m, 0m, 100m, 0m, 20m, 0, 0, 0m, 7m));
        Assert.Equal(144m, resultado.Proventos.Single(verba => verba.Descricao.StartsWith("Horas extras noturnas")).Valor);
    }

    [Fact]
    public async Task Trabalho_rural_tem_25_por_cento_sem_hora_reduzida()
    {
        // R$ 10,00 por hora: 7 horas noturnas rurais x 25% = R$ 17,50; 7 extras noturnas x 1,25 x 1,5 = R$ 131,25.
        var resultado = await CalcularAsync(new SimularHorasExtrasRequest(new DateOnly(2026, 10, 1), 2200m, 220m, 0m, 50m, 0m, 100m, 7m, 25m, 0, 0, 0m, 7m, Rural: true));
        Assert.Equal(17.50m, resultado.Proventos.Single(verba => verba.Descricao.StartsWith("Adicional noturno")).Valor);
        Assert.Equal(131.25m, resultado.Proventos.Single(verba => verba.Descricao.StartsWith("Horas extras noturnas")).Valor);
        await ResultadoAsync(new SimularHorasExtrasRequest(new DateOnly(2026, 10, 1), 2200m, 220m, 0m, 50m, 0m, 100m, 7m, 20m, 0, 0, Rural: true)).Falha();
    }

    [Fact]
    public async Task Adicionais_salariais_entram_no_valor_da_hora()
    {
        var resultado = await CalcularAsync(new SimularHorasExtrasRequest(new DateOnly(2026, 10, 1), 3000m, 220m, 10m, 50m, 0m, 100m, 0m, 20m, 0, 0, 324.20m));
        Assert.Equal(226.65m, resultado.Proventos.Single(verba => verba.Descricao == "Horas extras 50%").Valor);
        Assert.Equal(33.58m, resultado.Proventos.Single(verba => verba.Descricao.StartsWith("DSR")).Valor);
    }

    [Fact]
    public async Task Meio_centavo_arredonda_para_cima()
    {
        var faixa = await CalcularAsync(new SimularHorasExtrasRequest(new DateOnly(2026, 10, 1), 1535.10m, 180m, 1.5m, 100m, 0m, 100m, 0m, 20m, 0, 0));
        Assert.Equal(25.59m, faixa.Proventos[1].Valor);
        var dsr = await CalcularAsync(new SimularHorasExtrasRequest(new DateOnly(2023, 4, 1), 1756.72m, 220m, 1.5m, 62.5m, 0m, 100m, 0m, 20m, 1, 0));
        Assert.Equal(4.87m, dsr.Proventos.Single(verba => verba.Descricao.StartsWith("DSR")).Valor);
    }

    [Fact]
    public async Task Faixa_sem_horas_nao_exige_o_adicional_minimo()
    {
        await CalcularAsync(new SimularHorasExtrasRequest(new DateOnly(2026, 10, 1), 2200m, 220m, 10m, 50m, 0m, 0m, 0m, 20m, 0, 0));
        await ResultadoAsync(new SimularHorasExtrasRequest(new DateOnly(2026, 10, 1), 2200m, 220m, 10m, 40m, 0m, 100m, 0m, 20m, 0, 0)).Falha();
    }
}

public class DecimoTerceiroTests
{
    [Fact]
    public async Task Irrf_do_13o_e_descontado_mesmo_abaixo_de_dez_reais()
    {
        var resultado = await new SimularDecimoTerceiroUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularDecimoTerceiroRequest(new DateOnly(2025, 12, 1), 3100m, 0m, 12, 0, AdiantamentoDecimoTerceiro.CinquentaPorCento, 0m), default).Sucesso();
        Assert.Equal(4.80m, resultado.Descontos.Single(verba => verba.Descricao.StartsWith("IRRF")).Valor);
    }

    [Fact]
    public async Task Avisa_quando_a_segunda_parcela_fica_negativa()
    {
        var resultado = await new SimularDecimoTerceiroUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularDecimoTerceiroRequest(new DateOnly(2026, 12, 1), 8500m, 0m, 12, 0, AdiantamentoDecimoTerceiro.CinquentaPorCento, 0m, new RegraPensao(BasePensao.RendimentosBrutos, 50m, 0m)), default).Sucesso();
        Assert.StartsWith("A 2ª parcela ficou negativa", resultado.Observacoes[0]);
    }

    private static Task<DemonstrativoDto> ComPrevidenciaAsync(decimal previdencia) =>
        Ambiente.ConsultaAsync().ContinueWith(consulta => new SimularDecimoTerceiroUseCase(consulta.Result)
            .ExecutarAsync(new SimularDecimoTerceiroRequest(new DateOnly(2026, 12, 1), 8000m, 0m, 12, 0, AdiantamentoDecimoTerceiro.SemAdiantamento, 0m, null, previdencia), default).Sucesso()).Unwrap();

    [Theory]
    [InlineData(500, 500)]      // Cabe nos 12% de 8.000,00 (960,00): deduzida por inteiro.
    [InlineData(1500, 960)]     // Passa do limite: só 960,00 reduzem o IRRF do 13º.
    public async Task Previdencia_complementar_no_13o_limitada_a_12_por_cento(decimal previdencia, decimal deduzida)
    {
        var resultado = await ComPrevidenciaAsync(previdencia);

        var inss = ModeloTributario.Calcular(new DateOnly(2026, 10, 1), 8000m, 0).Inss;
        var irrf = ModeloTributario.Arredondar((8000m - inss - deduzida) * .275m - 908.73m);
        Assert.Equal(irrf, resultado.Descontos.Single(verba => verba.Descricao.StartsWith("IRRF")).Valor);
        Assert.Equal(previdencia, resultado.Descontos.Single(verba => verba.Descricao == "Previdência complementar sobre o 13º").Valor);
        Assert.Equal(8000m - inss - irrf - previdencia, resultado.Resultado);
        Assert.Equal($"Dedução no IRRF: R$ {deduzida.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}", Texto.Normalizar(resultado.Memoria.Single(grupo => grupo.Titulo == "Previdência complementar sobre o 13º").Destaque));
    }
}

public class FeriasTests
{
    [Fact]
    public void Rateio_respeita_fevereiro_bissexto_e_conserva_centavos()
    {
        var parcelas = RateioFeriasPorCompetencia.Calcular(new DateOnly(2028, 2, 28), 3, 100m, 33.33m).Sucesso();
        Assert.Equal([(new DateOnly(2028, 2, 1), 2), (new DateOnly(2028, 3, 1), 1)],
            parcelas.Select(item => (item.Competencia, item.Dias)));
        Assert.Equal(100m, parcelas.Sum(item => item.Ferias));
        Assert.Equal(33.33m, parcelas.Sum(item => item.Terco));
        Assert.True(RateioFeriasPorCompetencia.Calcular(DateOnly.MaxValue, 2, 100m, 33.33m).Falhou);
    }

    [Fact]
    public async Task Formulario_preserva_datas_e_bases_por_mes_e_reabre_historico_antigo()
    {
        var simulador = new SimularFeriasUseCase(await Ambiente.ConsultaAsync());
        var formulario = new CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras.CalculadoraFerias(simulador);
        formulario.ImportarCampos(new Dictionary<string, string>
        {
            ["Competência do pagamento"] = "01/2026", ["Início do gozo (opcional)"] = "20/01/2026",
            ["Salário"] = "3.000,00", ["Dias de descanso"] = "20", ["Base fora das férias (R$)"] = "1.800,00",
            ["Base fora das férias: 2º mês"] = "2.200,00"
        });
        var reaberto = new CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras.CalculadoraFerias(simulador);
        reaberto.ImportarCampos(formulario.ExportarCampos());
        Assert.Equal("20/01/2026", reaberto.ExportarCampos()["Início do gozo (opcional)"]);
        Assert.Contains((await reaberto.CalcularAsync(default)).Sucesso().Memoria, item => item.Titulo == "Folha de 02/2026");

        var antigo = new CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras.CalculadoraFerias(simulador);
        antigo.ImportarCampos(new Dictionary<string, string> { ["Competência do pagamento"] = "10/2026", ["Salário"] = "3.000,00" });
        Assert.Equal("", antigo.ExportarCampos()["Início do gozo (opcional)"]);
        Assert.DoesNotContain((await antigo.CalcularAsync(default)).Sucesso().Memoria, item => item.Titulo.StartsWith("Folha de "));
    }

    [Fact]
    public async Task Pagamento_em_dezembro_e_gozo_em_janeiro_usam_tabelas_distintas()
    {
        var pagamento = new DateOnly(2025, 12, 1);
        var gozo = new DateOnly(2026, 1, 5);
        var resultado = await new SimularFeriasUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularFeriasRequest(pagamento, 3000m, 0m, 0, 15, false, false, 0,
                BaseSalarialForaFeriasNoMes: 1500m, InicioGozo: gozo), default).Sucesso();

        // Férias R$ 1.500 + terço R$ 500, ambos na folha de janeiro; IRRF permanece no mês do pagamento.
        var tabela2026 = new DateOnly(2026, 10, 1); // Mesmas faixas de INSS vigentes em janeiro.
        var provisao = ModeloTributario.Calcular(tabela2026, 2000m, 0).Inss;
        var total = ModeloTributario.Calcular(tabela2026, 3500m, 0).Inss;
        Assert.Equal(provisao, resultado.Descontos.Single(item => item.Descricao == "INSS sobre as férias").Valor);
        Assert.Contains(resultado.Memoria.Single(item => item.Titulo == "Folha de 01/2026").Formulas,
            item => item.Titulo == "Saldo após provisão" && item.Formula.Contains((total - provisao).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))));
        Assert.Contains("Pagamento 12/2025", resultado.Referencia);
    }

    [Fact]
    public async Task Gozo_em_dois_meses_concilia_cada_folha_e_preserva_o_total_do_recibo()
    {
        var resultado = await new SimularFeriasUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularFeriasRequest(new DateOnly(2026, 1, 1), 3000m, 0m, 0, 20, false, false, 0,
                BaseSalarialForaFeriasNoMes: 1800m, InicioGozo: new DateOnly(2026, 1, 20),
                BaseForaFeriasMesSeguinte: 2200m), default).Sucesso();

        // Janeiro: 12 dias, férias R$ 1.200 + 1/3 R$ 400. Fevereiro: 8 dias, R$ 800 + R$ 266,67.
        var tabela2026 = new DateOnly(2026, 10, 1);
        var provisao = ModeloTributario.Calcular(tabela2026, 1600m, 0).Inss
            + ModeloTributario.Calcular(tabela2026, 1066.67m, 0).Inss;
        Assert.Equal(provisao, resultado.Descontos.Single(item => item.Descricao == "INSS sobre as férias").Valor);
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        Assert.Equal(ModeloTributario.Calcular(tabela2026, 3400m, 0).Inss.ToString("C", cultura),
            resultado.Destaques.Single(item => item.Rotulo == "INSS folha 01/2026").Valor);
        Assert.Equal(ModeloTributario.Calcular(tabela2026, 3266.67m, 0).Inss.ToString("C", cultura),
            resultado.Destaques.Single(item => item.Rotulo == "INSS folha 02/2026").Valor);
        Assert.Contains(resultado.Memoria.Single(item => item.Titulo == "Folha de 01/2026").Formulas,
            item => item.Titulo == "Base do INSS" && item.Formula.Contains("3.400,00"));
        Assert.Contains(resultado.Memoria.Single(item => item.Titulo == "Folha de 02/2026").Formulas,
            item => item.Titulo == "Base do INSS" && item.Formula.Contains("3.266,67"));
        Assert.Equal(2666.67m, resultado.Proventos.Where(item => item.Descricao is "Férias" or "1/3 constitucional sobre as férias").Sum(item => item.Valor));
    }

    [Fact]
    public async Task Inicio_no_fim_de_janeiro_pode_alcancar_tres_competencias()
    {
        var resultado = await new SimularFeriasUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularFeriasRequest(new DateOnly(2026, 1, 1), 3000m, 0m, 0, 30, false, false, 0,
                InicioGozo: new DateOnly(2026, 1, 31)), default).Sucesso();
        Assert.Contains(resultado.Memoria, item => item.Titulo == "Folha de 01/2026");
        Assert.Contains(resultado.Memoria, item => item.Titulo == "Folha de 02/2026");
        Assert.Contains(resultado.Memoria, item => item.Titulo == "Folha de 03/2026");
        Assert.Equal(30, resultado.Memoria.Where(item => item.Titulo.StartsWith("Folha de ")).SelectMany(item => item.Formulas)
            .Where(item => item.Titulo == "Dias de gozo").Sum(item => int.Parse(item.Formula.Split(' ')[0])));
    }

    [Fact]
    public async Task Bases_de_meses_sem_gozo_sao_rejeitadas()
    {
        var caso = new SimularFeriasUseCase(await Ambiente.ConsultaAsync());
        Assert.True((await caso.ExecutarAsync(new SimularFeriasRequest(new DateOnly(2026, 1, 1), 3000m, 0m, 0, 15, false, false, 0,
            BaseForaFeriasMesSeguinte: 100m), default)).Falhou);
        Assert.True((await caso.ExecutarAsync(new SimularFeriasRequest(new DateOnly(2026, 1, 1), 3000m, 0m, 0, 15, false, false, 0,
            InicioGozo: new DateOnly(2026, 1, 5), BaseForaFeriasMesSeguinte: 100m), default)).Falhou);
    }

    [Fact]
    public async Task Concilia_inss_de_ferias_e_salario_da_mesma_competencia()
    {
        var competencia = new DateOnly(2026, 10, 1);
        var resultado = await new SimularFeriasUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularFeriasRequest(competencia, 6000m, 0m, 0, 15, false, false, 0,
                BaseSalarialForaFeriasNoMes: 3000m), default).Sucesso();

        // 15 dias: R$ 3.000 de férias + R$ 1.000 de terço; com R$ 3.000 de salário, base mensal R$ 7.000.
        var provisao = ModeloTributario.Calcular(competencia, 4000m, 0).Inss;
        var total = ModeloTributario.Calcular(competencia, 7000m, 0).Inss;
        Assert.Equal(provisao, resultado.Descontos.Single(verba => verba.Descricao == "INSS sobre as férias").Valor);
        Assert.Equal(total.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
            resultado.Destaques.Single(item => item.Rotulo == "INSS da folha do mês").Valor);
        Assert.Contains(resultado.Memoria.Single(item => item.Titulo == "Conciliação do INSS na folha do mês").Formulas,
            item => item.Titulo == "Saldo a descontar na folha" && item.Formula.Contains((total - provisao).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))));
        Assert.Contains(resultado.Observacoes, texto => texto.Contains("pagamento e gozo na mesma competência"));
    }

    [Fact]
    public async Task Concilia_ferias_sem_salario_fora_da_base_apenas_quando_informado()
    {
        var resultado = await new SimularFeriasUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularFeriasRequest(new DateOnly(2026, 10, 1), 3000m, 0m, 0, 30, false, false, 0), default).Sucesso();
        Assert.DoesNotContain(resultado.Destaques, item => item.Rotulo == "INSS da folha do mês");
        Assert.Contains(resultado.Observacoes, texto => texto.Contains("confira o INSS da base reunida"));
    }

    [Fact]
    public async Task Previdencia_complementar_nas_ferias_deduzida_por_inteiro()
    {
        var resultado = await new SimularFeriasUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularFeriasRequest(new DateOnly(2026, 10, 1), 6000m, 0m, 0, 0, false, false, 0, null, 600m), default).Sucesso();

        // Férias + 1/3 = 8.000,00; os 600,00 (7,5%) saem inteiros da base, sem o limite de 12% na fonte.
        var inss = ModeloTributario.Calcular(new DateOnly(2026, 10, 1), 8000m, 0).Inss;
        var irrf = ModeloTributario.Arredondar((8000m - inss - 600m) * .275m - 908.73m);
        Assert.Equal(inss, resultado.Descontos.Single(verba => verba.Descricao == "INSS sobre as férias").Valor);
        Assert.Equal(irrf, resultado.Descontos.Single(verba => verba.Descricao.StartsWith("IRRF")).Valor);
        Assert.Equal(8000m - inss - irrf - 600m, resultado.Resultado);
        Assert.Equal("Proventos menos INSS, IRRF e previdência", resultado.Destaques[0].Complemento);
    }

    [Fact]
    public async Task Barra_previdencia_maior_que_as_ferias()
    {
        var consulta = await Ambiente.ConsultaAsync();
        await new SimularFeriasUseCase(consulta)
            .ExecutarAsync(new SimularFeriasRequest(new DateOnly(2026, 10, 1), 3000m, 0m, 0, 0, false, false, 0, null, 5000m), default).Falha();
        await new SimularFeriasUseCase(consulta)
            .ExecutarAsync(new SimularFeriasRequest(new DateOnly(2026, 10, 1), 3000m, 0m, 0, 0, false, false, 0, null, -1m), default).Falha();
    }
}

public class PlrTests
{
    [Fact]
    public async Task Imposto_ja_retido_no_ano_nao_e_confundido_com_faixa_isenta()
    {
        var resultado = await new SimularPlrUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularPlrRequest(new DateOnly(2026, 10, 1), 1000m, 20_000m, 3000m), default).Sucesso();
        Assert.Equal("O imposto do ano já foi retido nas parcelas anteriores", resultado.Destaques[1].Complemento);
    }

    [Theory]
    [InlineData(8214.40, 0)]
    [InlineData(9922.28, 128.09)]
    [InlineData(10_000.00, 139.75)]
    [InlineData(20_000.00, 2333.20)]
    public async Task Usa_a_tabela_anual_da_plr(decimal valor, decimal imposto)
    {
        var resultado = await new SimularPlrUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularPlrRequest(new DateOnly(2026, 10, 1), valor, 0m, 0m), default).Sucesso();
        Assert.Equal(imposto, resultado.Descontos.Sum(verba => verba.Valor));
    }
}

public class CustoFuncionarioTests
{
    [Fact]
    public async Task Soma_encargos_e_provisoes_e_explica_o_rat_com_o_fap()
    {
        var resultado = await new SimularCustoFuncionarioUseCase().ExecutarAsync(new SimularCustoFuncionarioRequest(3000m, RegimeTributario.LucroRealOuPresumido, 2m, 1.2345m, 5.8m, 0m, true), default).Sucesso();
        var formulas = resultado.Memoria.SelectMany(grupo => grupo.Formulas).Select(formula => Texto.Normalizar(formula.Formula)).ToArray();
        Assert.Contains(formulas, formula => formula == "20% (patronal) + 2,469% (RAT 2% x FAP 1,2345) + 5,8% (terceiros) + 8% (FGTS) = 36,269%");
        Assert.Contains(formulas, formula => formula.EndsWith("= R$ 4.882,97"));
    }

    [Fact]
    public async Task No_simples_so_entra_o_fgts()
    {
        var resultado = await new SimularCustoFuncionarioUseCase().ExecutarAsync(new SimularCustoFuncionarioRequest(3000m, RegimeTributario.SimplesNacional, 2m, 1m, 0m, 0m, false), default).Sucesso();
        Assert.Contains(resultado.Memoria.SelectMany(grupo => grupo.Formulas), formula => formula.Formula == "8% (FGTS) = 8,00%");
    }
}

public class SalarioFamiliaTests
{
    [Theory]
    [InlineData(1980.38, 30, 135.08)]
    [InlineData(1980.39, 30, 0)]
    [InlineData(1900.00, 18, 81.05)]
    public async Task Cota_de_2026_ate_o_limite_proporcional_aos_dias(decimal remuneracao, int dias, decimal valor)
    {
        var resultado = await new SimularSalarioFamiliaUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularSalarioFamiliaRequest(new DateOnly(2026, 10, 1), remuneracao, 2, dias), default).Sucesso();
        Assert.Equal(valor, resultado.Proventos.Sum(verba => verba.Valor));
    }
}
