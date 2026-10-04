using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Tests.Referencia;
using Xunit;
using CalculosTrabalhistasTributarios.Domain.Comum;

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
