using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class SeguroDesempregoTests
{
    private static async Task<DemonstrativoDto> CalcularAsync(string dispensa, decimal[] salarios, SolicitacaoSeguroDesemprego solicitacao, int meses) =>
        await new SimularSeguroDesempregoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularSeguroDesempregoRequest(DateOnly.Parse(dispensa), salarios, solicitacao, meses,
                MesesNoPeriodoCarencia: Math.Min(meses, RegrasSeguroDesemprego.MesesPeriodoCarencia(solicitacao))), default).Sucesso();

    // Tabela de 2026: até 2.222,17, 80% da média; até 3.703,99, 1.777,74 + 50% do excedente; acima, 2.518,65. Piso: 1.621,00.
    [Theory]
    [InlineData(2000.00, 1621.00)]
    [InlineData(2222.17, 1777.74)]
    [InlineData(3000.00, 2166.66)]
    [InlineData(3703.99, 2518.65)]
    [InlineData(5000.00, 2518.65)]
    public async Task Valor_da_parcela_pela_tabela_de_2026(decimal media, decimal parcela)
    {
        var resultado = await CalcularAsync("2026-10-15", [media, media, media], SolicitacaoSeguroDesemprego.Primeira, 30);
        Assert.Equal(parcela * 5, resultado.Proventos.Single().Valor);
    }

    [Fact]
    public async Task Media_usa_so_os_meses_com_salario()
    {
        var resultado = await CalcularAsync("2026-10-15", [3000m, 2500m, 0m], SolicitacaoSeguroDesemprego.Primeira, 12);
        // (3.000 + 2.500) ÷ 2 = 2.750: 1.777,74 + 263,915 = 2.041,66, em 4 parcelas.
        Assert.Equal(2041.66m * 4, resultado.Proventos.Single().Valor);
    }

    [Fact]
    public async Task Usa_a_tabela_vigente_na_dispensa()
    {
        var resultado = await CalcularAsync("2025-06-15", [3000m], SolicitacaoSeguroDesemprego.Primeira, 24);
        Assert.Equal(2141.63m * 5, resultado.Proventos.Single().Valor);
    }

    [Theory]
    [InlineData(SolicitacaoSeguroDesemprego.Primeira, 11, 0)]
    [InlineData(SolicitacaoSeguroDesemprego.Primeira, 12, 4)]
    [InlineData(SolicitacaoSeguroDesemprego.Primeira, 24, 5)]
    [InlineData(SolicitacaoSeguroDesemprego.Segunda, 8, 0)]
    [InlineData(SolicitacaoSeguroDesemprego.Segunda, 9, 3)]
    [InlineData(SolicitacaoSeguroDesemprego.Segunda, 23, 4)]
    [InlineData(SolicitacaoSeguroDesemprego.TerceiraOuMais, 5, 0)]
    [InlineData(SolicitacaoSeguroDesemprego.TerceiraOuMais, 6, 3)]
    [InlineData(SolicitacaoSeguroDesemprego.TerceiraOuMais, 60, 5)]
    public void Parcelas_pela_solicitacao_e_pelos_meses(SolicitacaoSeguroDesemprego solicitacao, int meses, int parcelas) =>
        Assert.Equal(parcelas, RegrasSeguroDesemprego.Parcelas(solicitacao, meses,
            Math.Min(meses, RegrasSeguroDesemprego.MesesPeriodoCarencia(solicitacao))));

    [Theory]
    [InlineData(SolicitacaoSeguroDesemprego.Primeira, 24, 11, 18)]
    [InlineData(SolicitacaoSeguroDesemprego.Segunda, 24, 8, 12)]
    [InlineData(SolicitacaoSeguroDesemprego.TerceiraOuMais, 24, 5, 6)]
    public async Task Meses_fora_da_janela_nao_cumprem_carencia(SolicitacaoSeguroDesemprego solicitacao, int total, int carencia, int periodo)
    {
        var resultado = await new SimularSeguroDesempregoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularSeguroDesempregoRequest(new DateOnly(2026, 10, 15), [3000m], solicitacao, total,
                MesesNoPeriodoCarencia: carencia), default).Sucesso();

        Assert.Empty(resultado.Proventos);
        Assert.Equal("Sem direito", resultado.Destaques[1].Valor);
        Assert.Contains(resultado.Memoria.SelectMany(grupo => grupo.Formulas), formula =>
            formula.Titulo == "Carência" && formula.Formula.Contains($"{carencia} mês(es) com salário nos últimos {periodo}"));
    }

    [Fact]
    public async Task Historico_sem_contagem_da_carencia_exige_preenchimento()
    {
        var calculadora = new CalculadoraSeguroDesemprego(new SimularSeguroDesempregoUseCase(await Ambiente.ConsultaAsync()));
        calculadora.ImportarCampos(new Dictionary<string, string>
        {
            ["Data da dispensa"] = "15/10/2026", ["Salário do último mês"] = "3.000,00",
            ["Meses trabalhados"] = "24", ["Solicitação"] = "1ª solicitação"
        });

        var erro = (await calculadora.CalcularAsync(default)).Falha();
        Assert.Contains("Meses com salário (18)", erro.Mensagem);
        Assert.Equal(string.Empty, ((CampoTextoViewModel)calculadora.Campos.Single(campo => campo.Rotulo == "Meses com salário (18)")).Valor);
    }

    [Theory]
    [InlineData(SolicitacaoSeguroDesemprego.Primeira, 24, 19)]
    [InlineData(SolicitacaoSeguroDesemprego.Segunda, 8, 9)]
    [InlineData(SolicitacaoSeguroDesemprego.TerceiraOuMais, 24, 7)]
    public async Task Contagem_incoerente_e_rejeitada(SolicitacaoSeguroDesemprego solicitacao, int total, int carencia)
    {
        var erro = await new SimularSeguroDesempregoUseCase(await Ambiente.ConsultaAsync())
            .ExecutarAsync(new SimularSeguroDesempregoRequest(new DateOnly(2026, 10, 15), [3000m], solicitacao, total,
                MesesNoPeriodoCarencia: carencia), default).Falha();

        Assert.Contains("Meses com salário", erro.Mensagem);
    }

    [Fact]
    public async Task Formulario_mostra_apenas_a_janela_da_solicitacao()
    {
        var calculadora = new CalculadoraSeguroDesemprego(new SimularSeguroDesempregoUseCase(await Ambiente.ConsultaAsync()));
        calculadora.ImportarCampos(new Dictionary<string, string>
        {
            ["Solicitação"] = "3ª ou seguinte", ["Meses com salário (6)"] = "6"
        });

        Assert.True(calculadora.Campos.Single(campo => campo.Rotulo == "Meses com salário (6)").Visivel);
        Assert.False(calculadora.Campos.Single(campo => campo.Rotulo == "Meses com salário (18)").Visivel);
        Assert.Equal("6", calculadora.ExportarCampos()["Meses com salário (6)"]);
    }

    [Fact]
    public async Task Sem_carencia_mostra_que_nao_ha_direito()
    {
        var resultado = await CalcularAsync("2026-10-15", [3000m], SolicitacaoSeguroDesemprego.Primeira, 11);
        Assert.Empty(resultado.Proventos);
        Assert.Equal("Sem direito", resultado.Destaques[1].Valor);
    }
}

public class RescisaoComplementosTests
{
    private static async Task<Result<DemonstrativoDto>> ResultadoAsync(SimularRescisaoRequest request) =>
        await new SimularRescisaoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(request, default);

    private static Task<DemonstrativoDto> CalcularAsync(SimularRescisaoRequest request) => ResultadoAsync(request).Sucesso();

    private static decimal? Valor(IEnumerable<VerbaDto> verbas, string inicio) => verbas.FirstOrDefault(verba => verba.Descricao.StartsWith(inicio))?.Valor;

    // Contrato de experiência de 01/09/2026 a 29/11/2026, encerrado em 30/09/2026: faltavam 60 dias.
    private static SimularRescisaoRequest Antecipada(MotivoRescisao motivo) =>
        new(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), motivo, CumprimentoAvisoPrevio.TrabalhadoOuDispensado, 3000m, 0m, 0, 0, 0m, 0m, 0,
            FimPrevistoContrato: new DateOnly(2026, 11, 29));

    [Fact]
    public async Task Antecipada_pelo_empregador_paga_metade_dos_dias_restantes_e_a_multa_do_fgts()
    {
        var resultado = await CalcularAsync(Antecipada(MotivoRescisao.RescisaoAntecipadaPeloEmpregador));
        Assert.Equal(3000m, Valor(resultado.Proventos, "Indenização da rescisão antecipada"));
        Assert.Equal(250m, Valor(resultado.Proventos, "13º salário proporcional"));
        // FGTS de (3.000 de saldo + 250 de 13º) × 8%; a indenização do art. 479 não tem FGTS.
        Assert.Equal(260m, Valor(resultado.Informativos, "Depósito do FGTS"));
        Assert.Equal(104m, Valor(resultado.Informativos, "Multa rescisória"));
        Assert.Equal(364m, Valor(resultado.Informativos, "FGTS disponível"));
        // A indenização não entra na base do INSS: só o saldo de 3.000,00.
        Assert.Equal(248.58m, Valor(resultado.Descontos, "INSS sobre o saldo"));
    }

    [Fact]
    public async Task Antecipada_pelo_empregado_mostra_o_limite_da_indenizacao_sem_descontar()
    {
        var resultado = await CalcularAsync(Antecipada(MotivoRescisao.RescisaoAntecipadaPeloEmpregado));
        Assert.Equal(3000m, Valor(resultado.Informativos, "Indenização máxima ao empregador"));
        Assert.Null(Valor(resultado.Proventos, "Indenização da rescisão antecipada"));
        Assert.Null(Valor(resultado.Informativos, "Multa rescisória"));
        Assert.Null(Valor(resultado.Informativos, "FGTS disponível"));
    }

    [Fact]
    public async Task Antecipada_exige_o_fim_previsto_do_contrato()
    {
        var semFim = Antecipada(MotivoRescisao.RescisaoAntecipadaPeloEmpregador) with { FimPrevistoContrato = null };
        await ResultadoAsync(semFim).Falha();
    }

    [Theory]
    [InlineData(CumprimentoAvisoPrevio.TrabalhadoOuDispensado, true)]
    [InlineData(CumprimentoAvisoPrevio.Indenizado, false)]
    public async Task Indenizacao_adicional_nos_30_dias_antes_da_data_base(CumprimentoAvisoPrevio aviso, bool devida)
    {
        // Data-base em 01/05/2026. Sem projeção, o contrato termina em 05/04, dentro dos 30 dias anteriores; com 39 dias de
        // aviso indenizado, vai até 14/05 e passa da data-base (Súmulas 182 e 314 do TST).
        var resultado = await CalcularAsync(new SimularRescisaoRequest(new DateOnly(2023, 1, 10), new DateOnly(2026, 4, 5), MotivoRescisao.DispensaSemJustaCausa,
            aviso, 3000m, 0m, 0, 0, 0m, 0m, 0, MesDataBase: 5));
        Assert.Equal(devida ? 3000m : null, Valor(resultado.Proventos, "Indenização adicional"));
        Assert.Contains(resultado.Observacoes, observacao => observacao.Contains(devida ? "indenização adicional de um salário" : "Súmula 314"));
    }

    [Theory]
    [InlineData("2026-10-25", false)]
    [InlineData("2026-10-26", true)]
    public async Task Multa_do_art_477_depois_de_10_dias(string pagamento, bool devida)
    {
        var resultado = await CalcularAsync(new SimularRescisaoRequest(new DateOnly(2023, 1, 10), new DateOnly(2026, 10, 15), MotivoRescisao.PedidoDeDemissao,
            CumprimentoAvisoPrevio.TrabalhadoOuDispensado, 3000m, 0m, 0, 0, 0m, 0m, 0, DataPagamento: DateOnly.Parse(pagamento)));
        Assert.Equal(devida ? 3000m : null, Valor(resultado.Proventos, "Multa por atraso"));
    }

    [Fact]
    public async Task Faltas_e_outros_proventos_do_mes()
    {
        var resultado = await CalcularAsync(new SimularRescisaoRequest(new DateOnly(2023, 1, 10), new DateOnly(2026, 10, 20), MotivoRescisao.PedidoDeDemissao,
            CumprimentoAvisoPrevio.TrabalhadoOuDispensado, 3000m, 0m, 0, 0, 0m, 0m, 0, OutrosProventos: 500m, FaltasNoMes: 2));
        Assert.Equal(1800m, Valor(resultado.Proventos, "Saldo de salário"));
        Assert.Equal(500m, Valor(resultado.Proventos, "Outros proventos do mês"));
        // INSS de 2026 sobre 2.300,00: 121,57 + 61,11.
        Assert.Equal(182.68m, Valor(resultado.Descontos, "INSS sobre o saldo de salário e outros proventos"));
    }

    [Fact]
    public async Task Acordo_saca_80_por_cento_do_saldo_inclusive_da_multa()
    {
        // Manual de Movimentação do FGTS da Caixa, versão 28, código 07: 80% do saldo, inclusive da multa rescisória.
        var resultado = await CalcularAsync(new SimularRescisaoRequest(new DateOnly(2023, 1, 10), new DateOnly(2026, 10, 20), MotivoRescisao.Acordo,
            CumprimentoAvisoPrevio.Indenizado, 3000m, 0m, 0, 0, 10_000m, 0m, 0));
        var deposito = Valor(resultado.Informativos, "Depósito do FGTS")!.Value;
        var multa = Valor(resultado.Informativos, "Multa rescisória")!.Value;
        Assert.Equal(Math.Round((10_000m + deposito) * .2m, 2, MidpointRounding.AwayFromZero), multa);
        Assert.Equal(Math.Round((10_000m + deposito + multa) * .8m, 2, MidpointRounding.AwayFromZero), Valor(resultado.Informativos, "FGTS disponível"));
    }

    [Fact]
    public async Task Estima_o_seguro_desemprego_na_dispensa_sem_justa_causa()
    {
        var resultado = await CalcularAsync(new SimularRescisaoRequest(new DateOnly(2024, 1, 10), new DateOnly(2026, 10, 20), MotivoRescisao.DispensaSemJustaCausa,
            CumprimentoAvisoPrevio.Indenizado, 3000m, 0m, 0, 0, 0m, 0m, 0));
        // 33 meses de contrato: 5 parcelas de 2.166,66.
        Assert.Equal(2166.66m * 5, Valor(resultado.Informativos, "Seguro-desemprego"));
    }
}
