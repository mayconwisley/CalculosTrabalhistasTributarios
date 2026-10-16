using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using CalculosTrabalhistasTributarios.Tests.Referencia;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class SimulacaoTributariaTests
{
    public static TheoryData<string> Competencias => new(ModeloTributario.Competencias.Select(competencia => competencia.ToString("yyyy-MM")));

    private static async Task<Result<DemonstrativoDto>> SimularAsync(SimularImpostoRequest request) =>
        await new SimularImpostoUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(request, default);

    private static Task<Result<DemonstrativoDto>> SimularAsync(string mes, decimal bruto, int dependentes = 0, VinculoSimulacao vinculo = VinculoSimulacao.Empregado) =>
        SimularAsync(new SimularImpostoRequest(DateOnly.ParseExact(mes + "-01", "yyyy-MM-dd"), bruto, bruto, dependentes, vinculo));

    private static decimal Inss(DemonstrativoDto demonstrativo) => demonstrativo.Descontos.Single(verba => verba.Descricao == "INSS").Valor;
    private static decimal Irrf(DemonstrativoDto demonstrativo) => demonstrativo.Descontos.Single(verba => verba.Descricao.StartsWith("IRRF", StringComparison.Ordinal)).Valor;
    private static string Destaque(DemonstrativoDto demonstrativo, string rotulo) => Texto.Normalizar(demonstrativo.Destaques.Single(destaque => destaque.Rotulo == rotulo).Valor);
    private static string Complemento(DemonstrativoDto demonstrativo, string rotulo) => Texto.Normalizar(demonstrativo.Destaques.Single(destaque => destaque.Rotulo == rotulo).Complemento);

    /// <summary>IRRF da modalidade na tabela comparativa: deduções legais na primeira linha, desconto simplificado na segunda.</summary>
    private static string ImpostoDaModalidade(DemonstrativoDto demonstrativo, int linha) => Texto.Normalizar(demonstrativo.Comparativo!.Linhas[linha].Valores[2]);

    private static string Moeda(decimal valor) => Texto.Normalizar(valor.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")));

    [Theory]
    [MemberData(nameof(Competencias))]
    public async Task Bate_com_o_modelo_de_referencia_em_toda_a_faixa_de_salarios(string mes)
    {
        var competencia = DateOnly.ParseExact(mes + "-01", "yyyy-MM-dd");
        var simulador = new SimularImpostoUseCase(await Ambiente.ConsultaAsync());
        var diferencas = new List<string>();
        for (var bruto = 0m; bruto <= 12_000m; bruto += 7.31m)
            foreach (var dependentes in new[] { 0, 1, 3 })
            {
                var app = await simulador.ExecutarAsync(new SimularImpostoRequest(competencia, bruto, bruto, dependentes), default).Sucesso();
                var esperado = ModeloTributario.Calcular(competencia, bruto, dependentes);
                if (Inss(app) != esperado.Inss || ImpostoDaModalidade(app, 0) != Moeda(esperado.ImpostoNormal) || Irrf(app) != esperado.IrrfRetido || app.Resultado != esperado.Liquido
                    || esperado.ImpostoSimplificado is { } simplificado && ImpostoDaModalidade(app, 1) != Moeda(simplificado))
                    diferencas.Add($"{bruto} com {dependentes} dependente(s): app {Inss(app)}/{ImpostoDaModalidade(app, 0)}/{Irrf(app)}, esperado {esperado}");
            }
        Assert.Empty(diferencas.Take(10));
    }

    [Theory]
    [InlineData("2026-10", 8475.55, 988.07)]
    [InlineData("2026-10", 9000.00, 988.07)]
    [InlineData("2026-10", 5000.00, 501.50)]
    [InlineData("2026-10", 3000.00, 248.58)]
    [InlineData("2021-06", 6433.57, 751.97)]
    [InlineData("2019-06", 3000.00, 330.00)]
    public async Task Inss_trunca_cada_faixa_como_o_eSocial(string mes, decimal bruto, decimal inss)
    {
        var simulacao = await SimularAsync(mes, bruto).Sucesso();
        Assert.Equal(inss, Inss(simulacao));
    }

    [Theory]
    [InlineData("2026-10", 8500.00, "14%")]
    [InlineData("2026-10", 3000.00, "12%")]
    [InlineData("2026-10", 1000.00, "7,5%")]
    [InlineData("2019-06", 3000.00, "11%")]
    [InlineData("2026-10", 0.00, "")]
    public async Task Referencia_do_inss_e_a_aliquota_da_ultima_faixa_alcancada(string mes, decimal bruto, string referencia)
    {
        // Em 10/2026, R$ 3.000,00 passa do limite de R$ 2.902,84 da faixa de 9% e chega à de 12%; antes de 03/2020, a alíquota
        // é única: R$ 3.000,00 em 06/2019 fica na faixa de 11%.
        var simulacao = await SimularAsync(mes, bruto).Sucesso();
        Assert.Equal(referencia, simulacao.Descontos.Single(verba => verba.Descricao == "INSS").Referencia);
    }

    [Theory]
    [InlineData("2025-06", 3100.00, 4.80, true, 2834.60)]
    [InlineData("2026-10", 5010.00, 3.58, true, 4507.10)]
    [InlineData("2026-10", 5040.00, 14.32, false, 4518.58)]
    public async Task Irrf_de_ate_dez_reais_nao_e_retido(string mes, decimal bruto, decimal calculado, bool dispensado, decimal liquido)
    {
        var simulacao = await SimularAsync(mes, bruto).Sucesso();
        Assert.Equal(dispensado ? 0m : calculado, Irrf(simulacao));
        Assert.Equal(liquido, simulacao.Resultado);
        Assert.Equal(Moeda(liquido), Destaque(simulacao, "Salário líquido"));
        if (dispensado)
            Assert.Equal($"IRRF de {Moeda(calculado)} dispensado", Complemento(simulacao, "Salário líquido"));
    }

    [Fact]
    public async Task Antes_de_maio_de_2023_nao_ha_desconto_simplificado()
    {
        var abril = await SimularAsync("2023-04", 5000m).Sucesso();
        var maio = await SimularAsync("2023-05", 5000m).Sucesso();
        Assert.Single(abril.Comparativo!.Linhas);
        Assert.Equal("IRRF com deduções legais", abril.Comparativo.Titulo);
        Assert.Equal(2, maio.Comparativo!.Linhas.Count);
        // Base simplificada de maio de 2023: R$ 5.000,00 menos o desconto de R$ 528,00.
        Assert.Equal("R$ 4.472,00", Texto.Normalizar(maio.Comparativo.Linhas[1].Valores[0]));
    }

    [Fact]
    public async Task Modalidade_de_menor_imposto_e_a_aplicada_e_a_diferenca_aparece_em_reais()
    {
        // 06/2025, R$ 3.100,00: INSS de R$ 265,40; deduções legais com base de R$ 2.834,60 e IRRF de R$ 31,03; desconto
        // simplificado com base de R$ 2.492,80 e IRRF de R$ 4,80. A diferença é de R$ 26,23.
        var simulacao = await SimularAsync("2025-06", 3100m).Sucesso();
        Assert.Equal("R$ 31,03", ImpostoDaModalidade(simulacao, 0));
        Assert.Equal("R$ 4,80", ImpostoDaModalidade(simulacao, 1));
        Assert.True(simulacao.Comparativo!.Linhas[1].Destaque);
        Assert.False(simulacao.Comparativo.Linhas[0].Destaque);
        Assert.Contains(simulacao.Observacoes, observacao => Texto.Normalizar(observacao).StartsWith("O desconto simplificado é mais vantajoso: R$ 26,23 a menos", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(VinculoSimulacao.Empregado, "FGTS (8%)", "R$ 400,00", 400.00, null)]
    [InlineData(VinculoSimulacao.Aprendiz, "FGTS (2%)", "R$ 100,00", 100.00, null)]
    [InlineData(VinculoSimulacao.Domestico, "FGTS e indenização", "R$ 560,00", 400.00, 160.00)]
    public async Task Fgts_segue_o_tipo_de_vinculo_e_nao_reduz_o_liquido(VinculoSimulacao vinculo, string rotulo, string destaque, decimal fgts, double? compensatoria)
    {
        // R$ 5.000,00 x 8% = R$ 400,00; x 2% = R$ 100,00; no doméstico, mais R$ 5.000,00 x 3,2% = R$ 160,00.
        var simulacao = await SimularAsync("2026-10", 5000m, vinculo: vinculo).Sucesso();
        var empregado = await SimularAsync("2026-10", 5000m).Sucesso();

        Assert.Equal(destaque, Destaque(simulacao, rotulo));
        Assert.Equal(fgts, simulacao.Informativos.Single(verba => verba.Descricao == "FGTS").Valor);
        Assert.Equal(compensatoria is { } valor ? (decimal?)(decimal)valor : null, simulacao.Informativos.SingleOrDefault(verba => verba.Descricao == "Indenização compensatória (FGTS)")?.Valor);
        Assert.Equal(empregado.Resultado, simulacao.Resultado);
        Assert.Equal(Inss(empregado), Inss(simulacao));
    }

    [Fact]
    public async Task Fgts_incide_sobre_toda_a_base_sem_o_teto_do_inss()
    {
        // R$ 12.000,00 x 8% = R$ 960,00, embora o INSS pare no teto de R$ 8.475,55.
        var simulacao = await SimularAsync("2026-10", 12_000m).Sucesso();
        Assert.Equal(960m, simulacao.Informativos.Single(verba => verba.Descricao == "FGTS").Valor);
        Assert.Equal("Base limitada ao teto de R$ 8.475,55", Complemento(simulacao, "INSS"));
    }

    [Fact]
    public async Task Base_de_inss_diferente_do_bruto_muda_o_inss_e_o_fgts_mas_nao_o_rendimento_do_irrf()
    {
        // Base de INSS de R$ 3.000,00 em 10/2026: INSS de R$ 248,58 e FGTS de R$ 240,00. O IRRF usa o bruto de R$ 4.000,00.
        var simulacao = await SimularAsync(new SimularImpostoRequest(new DateOnly(2026, 10, 1), 4000m, 3000m, 0)).Sucesso();
        Assert.Equal(248.58m, Inss(simulacao));
        Assert.Equal(240m, simulacao.Informativos.Single(verba => verba.Descricao == "FGTS").Valor);
        Assert.Equal(4000m, simulacao.TotalProventos);
        Assert.Contains(simulacao.Observacoes, observacao => observacao.StartsWith("A base de INSS", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-1, 0, 0, "Valor bruto:")]
    [InlineData(1000, -1, 0, "Base de INSS:")]
    [InlineData(1000, 1000, -1, "Dependentes:")]
    [InlineData(1000, 1000, 100, "Dependentes:")]
    [InlineData(1_000_000_001, 0, 0, "Valor bruto:")]
    public async Task Entrada_invalida_identifica_o_campo(double bruto, double baseInss, int dependentes, string campo)
    {
        var resultado = await SimularAsync(new SimularImpostoRequest(new DateOnly(2026, 10, 1), (decimal)bruto, (decimal)baseInss, dependentes));
        Assert.True(resultado.Falhou);
        Assert.Equal(TipoErro.Validacao, resultado.Erro.Tipo);
        Assert.StartsWith(campo, resultado.Erro.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vinculo_invalido_e_recusado()
    {
        var resultado = await SimularAsync(new SimularImpostoRequest(new DateOnly(2026, 10, 1), 1000m, 1000m, 0, (VinculoSimulacao)9));
        Assert.StartsWith("Tipo de vínculo:", resultado.Erro.Mensagem, StringComparison.Ordinal);
    }

    // Tabela de 10/2026: faixa de 27,5% com parcela a deduzir de R$ 908,73, desconto simplificado de R$ 607,20 e redução de
    // R$ 978,62 - 0,133145 x rendimentos tributáveis entre R$ 5.000,01 e R$ 7.350,00. Aos 65 anos, R$ 8.000,00 - R$ 1.903,98 =
    // R$ 6.096,02 tributáveis, redução de R$ 166,97; simplificado: R$ 5.488,82 x 27,5% - R$ 908,73 = R$ 600,70, menos a
    // redução, R$ 433,73; deduções legais: R$ 767,68 - R$ 166,97 = R$ 600,71. Antes dos 65, os R$ 8.000,00 não têm redução:
    // R$ 7.392,80 x 27,5% - R$ 908,73 = R$ 1.124,29.
    [Theory]
    [InlineData(true, "R$ 433,73", "R$ 7.566,27", "R$ 1.903,98", "R$ 6.096,02", "R$ 600,71")]
    [InlineData(false, "R$ 1.124,29", "R$ 6.875,71", "R$ 0,00", "R$ 8.000,00", "R$ 1.291,27")]
    public async Task Aposentadoria_exclui_a_parcela_isenta_dos_65_anos_nas_duas_modalidades(bool sessentaECinco, string irrf, string liquido, string isenta, string tributavel, string deducoesLegais)
    {
        var resultado = (await SimularAsync(new SimularImpostoRequest(new DateOnly(2026, 10, 1), 8000m, 0m, 0, VinculoSimulacao.Aposentadoria, sessentaECinco))).Valor;

        Assert.Equal(irrf, Destaque(resultado, "IRRF"));
        Assert.Equal(liquido, Destaque(resultado, "Benefício líquido"));
        Assert.Equal(isenta, Destaque(resultado, "Parcela isenta"));
        Assert.Equal(tributavel, Destaque(resultado, "Rendimento tributável"));
        Assert.Equal(deducoesLegais, ImpostoDaModalidade(resultado, 0));
        Assert.Equal(irrf, ImpostoDaModalidade(resultado, 1));
        // Sem INSS nem FGTS: o único desconto é o IRRF, e o líquido é o bruto menos ele.
        Assert.Equal(Irrf(resultado), Assert.Single(resultado.Descontos).Valor);
        Assert.Equal(8000m - Irrf(resultado), resultado.Resultado);
        Assert.DoesNotContain(resultado.Informativos, verba => verba.Descricao.StartsWith("FGTS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Parcela_isenta_limita_se_ao_beneficio_e_vale_antes_do_desconto_simplificado()
    {
        // R$ 1.500,00 cabem inteiros na parcela isenta: nada a tributar.
        var pequeno = (await SimularAsync(new SimularImpostoRequest(new DateOnly(2026, 10, 1), 1500m, 0m, 0, VinculoSimulacao.Aposentadoria, true))).Valor;
        Assert.Equal("R$ 1.500,00", Destaque(pequeno, "Parcela isenta"));
        Assert.Equal("R$ 0,00", Destaque(pequeno, "Rendimento tributável"));
        Assert.Equal(0m, Irrf(pequeno));

        // 06/2019, tabela de 2017, sem simplificado: R$ 5.000,00 - R$ 1.903,98 - R$ 189,59 (dependente) = R$ 2.906,43;
        // x 15% - R$ 354,80 = R$ 81,16.
        var antigo = (await SimularAsync(new SimularImpostoRequest(new DateOnly(2019, 6, 1), 5000m, 0m, 1, VinculoSimulacao.Aposentadoria, true))).Valor;
        Assert.Equal(81.16m, Irrf(antigo));
    }

    // O 13º tem parcela isenta própria: R$ 8.000,00 - R$ 1.903,98 = R$ 6.096,02, com o mesmo IRRF de R$ 433,73 do benefício
    // do mês (12/2026, redução de R$ 166,97 no simplificado).
    [Fact]
    public async Task Decimo_terceiro_da_aposentadoria_tem_parcela_isenta_propria()
    {
        var resultado = (await SimularAsync(new SimularImpostoRequest(new DateOnly(2026, 12, 1), 8000m, 0m, 0, VinculoSimulacao.Aposentadoria, true, true))).Valor;

        Assert.Equal("R$ 433,73", Destaque(resultado, "IRRF"));
        Assert.Equal("R$ 7.566,27", Destaque(resultado, "13º líquido"));
        Assert.Equal("R$ 1.903,98", Destaque(resultado, "Parcela isenta"));
        Assert.Equal("Própria do 13º", Complemento(resultado, "Parcela isenta"));
        Assert.Equal("13º da aposentadoria ou pensão", Assert.Single(resultado.Proventos).Descricao);
        Assert.Equal("13º líquido", resultado.RotuloResultado);
    }

    // 12/2019, tabela de 2017: R$ 3.850,00 - R$ 1.903,98 = R$ 1.946,02; x 7,5% - R$ 142,80 = R$ 3,15. No benefício do mês, o
    // IRRF de até R$ 10,00 é dispensado; no 13º, de tributação exclusiva, ele é retido.
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 3.15)]
    public async Task Decimo_terceiro_da_aposentadoria_nao_tem_dispensa_de_retencao(bool decimoTerceiro, decimal irrf)
    {
        var resultado = (await SimularAsync(new SimularImpostoRequest(new DateOnly(2019, 12, 1), 3850m, 0m, 0, VinculoSimulacao.Aposentadoria, true, decimoTerceiro))).Valor;
        Assert.Equal(irrf, Irrf(resultado));
    }

    [Fact]
    public async Task Decimo_terceiro_desta_simulacao_exige_aposentadoria()
    {
        var resultado = await SimularAsync(new SimularImpostoRequest(new DateOnly(2026, 12, 1), 3000m, 3000m, 0, VinculoSimulacao.Empregado, false, true));
        Assert.StartsWith("Rendimento:", resultado.Erro.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(VinculoSimulacao.Aposentadoria, 1000, false, "Base de INSS:")]
    [InlineData(VinculoSimulacao.Empregado, 1000, true, "65 anos ou mais:")]
    public async Task Aposentadoria_recusa_base_de_inss_e_a_parcela_isenta_exige_aposentadoria(VinculoSimulacao vinculo, double baseInss, bool sessentaECinco, string campo)
    {
        var resultado = await SimularAsync(new SimularImpostoRequest(new DateOnly(2026, 10, 1), 1000m, (decimal)baseInss, 0, vinculo, sessentaECinco));
        Assert.StartsWith(campo, resultado.Erro.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Formulario_da_aposentadoria_oculta_a_base_de_inss_e_pergunta_os_65_anos()
    {
        var calculadora = new CalculadoraSimulacaoTributaria(new SimularImpostoUseCase(await Ambiente.ConsultaAsync()));
        var campos = calculadora.Campos;
        var baseInss = (CampoTextoViewModel)campos[2];
        var vinculo = (CampoOpcaoViewModel)campos[4];
        var rendimento = (CampoOpcaoViewModel)campos[5];
        var sessentaECinco = (CampoOpcaoViewModel)campos[6];
        Assert.False(sessentaECinco.Visivel || rendimento.Visivel);

        calculadora.ImportarCampos(new Dictionary<string, string>
        {
            ["Competência"] = "10/2026", ["Valor bruto"] = "8.000,00", ["Tipo de vínculo"] = "Aposentadoria ou pensão", ["65 anos ou mais"] = "Sim"
        });

        Assert.False(baseInss.Visivel);
        Assert.True(sessentaECinco.Visivel && rendimento.Visivel);
        // A base de INSS acompanhou o valor bruto, mas fica oculta e não vai para o cálculo.
        Assert.Equal("8.000,00", baseInss.Valor);
        var resultado = await calculadora.CalcularAsync(default);
        Assert.Equal("R$ 433,73", Destaque(resultado.Valor, "IRRF"));

        vinculo.Selecionada = vinculo.Opcoes[0];
        Assert.True(baseInss.Visivel);
        Assert.False(sessentaECinco.Visivel || rendimento.Visivel);
    }

    [Fact]
    public async Task Formulario_marca_o_campo_em_formato_invalido()
    {
        var calculadora = new CalculadoraSimulacaoTributaria(new SimularImpostoUseCase(await Ambiente.ConsultaAsync()));
        var bruto = (CampoTextoViewModel)calculadora.Campos[1];
        bruto.Valor = "abc";

        var resultado = await calculadora.CalcularAsync(default);

        Assert.True(resultado.Falhou);
        Assert.Contains("\"Valor bruto\"", resultado.Erro.Mensagem);
        Assert.NotNull(bruto.MensagemErro);
    }

    [Fact]
    public void Base_de_inss_acompanha_o_valor_bruto_e_o_historico_preserva_a_base_salva()
    {
        var calculadora = new CalculadoraSimulacaoTributaria(null!);
        var bruto = (CampoTextoViewModel)calculadora.Campos[1];
        var baseInss = (CampoTextoViewModel)calculadora.Campos[2];
        bruto.Valor = "4.500,00";
        Assert.Equal("4.500,00", baseInss.Valor);

        calculadora.ImportarCampos(new Dictionary<string, string> { ["Valor bruto"] = "6.000,00", ["Base de INSS"] = "5.000,00" });
        Assert.Equal("6.000,00", bruto.Valor);
        Assert.Equal("5.000,00", baseInss.Valor);
    }

    [Fact]
    public async Task Historico_da_janela_antiga_reabre_na_janela_padrao()
    {
        // A janela própria salvava os campos pelo nome da propriedade e o tipo "SimulacaoTributaria".
        var dados = new DadosFormulario(new() { ["Competencia"] = "06/2025", ["ValorBruto"] = "3.100,00", ["BaseInss"] = "3.000,00", ["Dependentes"] = "1" }).ParaJson();
        var historico = await Ambiente.HistoricoAsync();
        var id = await historico.SalvarAsync(null, "SimulacaoTributaria", "Simulação tributária", "Simulação antiga", dados, default);

        // Um novo acesso ao banco, como ao abrir o aplicativo atualizado, migra o tipo.
        var banco = new BancoTributario($"Data Source={System.IO.Path.Combine(AppContext.BaseDirectory, "BancoDados", "calculoIrrf.db")}");
        var reaberto = await new SqliteHistoricoCalculos(banco).ObterAsync(id, default);
        Assert.Equal("Calculadora.SimulacaoTributaria", reaberto!.Tipo);

        var calculadora = new CalculadoraSimulacaoTributaria(new SimularImpostoUseCase(await Ambiente.ConsultaAsync()));
        calculadora.ImportarCampos(DadosFormulario.DeJson(reaberto.Dados).Campos);
        var exportados = calculadora.ExportarCampos();
        Assert.Equal("06/2025", exportados["Competência"]);
        Assert.Equal("3.100,00", exportados["Valor bruto"]);
        Assert.Equal("3.000,00", exportados["Base de INSS"]);
        Assert.Equal("1", exportados["Dependentes"]);
        Assert.Equal("Empregado (FGTS 8%)", exportados["Tipo de vínculo"]);
        Assert.True((await calculadora.CalcularAsync(default)).Sucesso);
        await historico.ExcluirAsync(id, default);
    }
}
