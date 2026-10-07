using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Xunit;
using RegraSimples = CalculosTrabalhistasTributarios.Domain.Tributacao.CalculadoraSimplesNacional;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Simples Nacional e fator r (Resolução CGSN 140/2018, arts. 21, 22 e 26, e Anexos I a V; Resolução CGSN 190/2026).
/// Os valores esperados foram calculados à mão pelas tabelas publicadas.
/// </summary>
public class SimplesNacionalTests
{
    private static MesSimples[] Meses(DateOnly primeiro, int quantidade, decimal receita, decimal folha) =>
        Enumerable.Range(0, quantidade).Select(indice => new MesSimples(primeiro.AddMonths(indice), receita, folha)).ToArray();

    private static EntradaSimples Entrada(DateOnly periodo, IReadOnlyList<MesSimples> meses, decimal receitaMes = 20_000m,
        AtividadeSimples atividade = AtividadeSimples.ServicosFatorR, DateOnly? inicio = null, decimal folhaMes = 0m, decimal remuneracoes = 0m) =>
        new(periodo, atividade, receitaMes, folhaMes, inicio, meses, remuneracoes);

    [Fact]
    public async Task Fator_r_abaixo_de_28_leva_ao_anexo_v_e_estima_o_pro_labore_que_falta()
    {
        // 12 × 20.000 = RBT12 de 240.000; folha de 60.000 → r = 25% → Anexo V, 2ª faixa:
        // (240.000 × 18% - 4.500) ÷ 240.000 = 16,125% → DAS de 3.225,00 sobre 20.000. No Anexo III: 7,3% → 1.460,00.
        // Faltam 240.000 × 28% - 60.000 = 7.200 em 12 meses: 600,00 por mês. INSS do sócio: 11% de 3.000 → de 3.600 = + 66,00;
        // IRRF zero nos dois casos pela redução de 2026. Saldo: 1.765,00 - 66,00 = 1.699,00.
        var demonstrativo = (await new SimularSimplesNacionalUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(new SimularSimplesNacionalRequest(
            Entrada(new DateOnly(2026, 10, 1), Meses(new DateOnly(2025, 10, 1), 12, 20_000m, 5_000m)), 3_000m, 0), default)).Sucesso();

        Assert.Equal(3_225m, demonstrativo.Descontos.Single().Valor);
        Assert.Equal("25,00%", demonstrativo.Destaques.Single(item => item.Rotulo == "Fator r").Valor);
        Assert.Equal(600m, demonstrativo.Informativos.Single(item => item.Descricao.StartsWith("Pró-labore adicional")).Valor);
        Assert.Equal(["1.460,00", "3.225,00"], demonstrativo.Comparativo!.Linhas[^1].Valores.Select(valor => valor.Replace("R$", "").Trim()));
        Assert.Contains(demonstrativo.Memoria, grupo => grupo.Destaque == "Saldo de R$ 1.699,00 por mês");
    }

    [Fact]
    public void Fator_r_de_28_ou_mais_leva_ao_anexo_iii()
    {
        var simples = RegraSimples.Calcular(Entrada(new DateOnly(2026, 10, 1), Meses(new DateOnly(2025, 10, 1), 12, 20_000m, 6_000m))).Sucesso();

        Assert.Equal(0.3m, simples.FatorR);
        Assert.Equal(AnexoSimples.III, simples.Aliquota.Anexo);
        Assert.Equal(1_460m, simples.Das);
        Assert.Equal(72_000m, simples.Fs12);
    }

    [Fact]
    public void A_partir_de_2027_a_janela_salta_o_mes_anterior_e_a_6a_faixa_cai_0_1_ponto()
    {
        var janela = RegraSimples.Janela(new DateOnly(2027, 3, 1), null);
        Assert.Equal(new DateOnly(2026, 2, 1), janela.Meses[0]);
        Assert.Equal(new DateOnly(2027, 1, 1), janela.Meses[^1]);
        Assert.Equal(12, janela.Meses.Count);
        Assert.Equal(new DateOnly(2026, 9, 1), RegraSimples.Janela(new DateOnly(2026, 10, 1), null).Meses[^1]);

        // Anexo III com RBT12 de 4,8 milhões: (4.800.000 × 33% - 648.000) ÷ 4.800.000 = 19,5%; com 32,9% em 2027, 19,4%.
        Assert.Equal(19.5m, SimplesNacional.Aliquota(AnexoSimples.III, 4_800_000m, new DateOnly(2026, 12, 1)).Sucesso().AliquotaEfetiva);
        Assert.Equal(19.4m, SimplesNacional.Aliquota(AnexoSimples.III, 4_800_000m, new DateOnly(2027, 1, 1)).Sucesso().AliquotaEfetiva);
        Assert.True(SimplesNacional.Aliquota(AnexoSimples.III, 100_000m, new DateOnly(2029, 1, 1)).Falhou);
    }

    [Fact]
    public void Inicio_de_atividade_ate_2026_usa_a_receita_do_mes_e_depois_a_media()
    {
        // 1º mês: 15.000 × 12 = 180.000; sem folha, fator r de 0,01 → Anexo V, 1ª faixa de 15,5%. Com folha, FSPA ÷ RPA.
        var primeiro = RegraSimples.Calcular(Entrada(new DateOnly(2026, 10, 1), [], 15_000m, inicio: new DateOnly(2026, 10, 1))).Sucesso();
        Assert.Equal((180_000m, 0.01m, AnexoSimples.V, 15.5m), (primeiro.Rbt12, primeiro.FatorR!.Value, primeiro.Aliquota.Anexo, primeiro.Aliquota.AliquotaEfetiva));
        Assert.Equal(AnexoSimples.III, RegraSimples.Calcular(Entrada(new DateOnly(2026, 10, 1), [], 15_000m, inicio: new DateOnly(2026, 10, 1), folhaMes: 5_000m)).Sucesso().Aliquota.Anexo);

        // 4º mês: (10.000 + 20.000 + 30.000) ÷ 3 × 12 = 240.000; folha 9.000 ÷ 3 × 12 = 36.000; r = 15%.
        var quarto = RegraSimples.Calcular(Entrada(new DateOnly(2026, 10, 1),
            [new(new DateOnly(2026, 7, 1), 10_000m, 3_000m), new(new DateOnly(2026, 8, 1), 20_000m, 3_000m), new(new DateOnly(2026, 9, 1), 30_000m, 3_000m)],
            inicio: new DateOnly(2026, 7, 1))).Sucesso();
        Assert.Equal(RegraReceitaSimples.MediaInicio, quarto.Janela.Regra);
        Assert.Equal((240_000m, 36_000m, 0.15m), (quarto.Rbt12, quarto.Fs12, quarto.FatorR!.Value));
        Assert.Equal(16.125m, quarto.Aliquota.AliquotaEfetiva);
    }

    [Fact]
    public void Inicio_de_atividade_a_partir_de_2027_usa_a_1a_faixa_nos_dois_primeiros_meses()
    {
        var segundo = RegraSimples.Calcular(Entrada(new DateOnly(2027, 2, 1), [], 10_000m, inicio: new DateOnly(2027, 1, 1))).Sucesso();
        Assert.Equal((RegraReceitaSimples.PrimeiraFaixa, 0.28m, AnexoSimples.III, 600m), (segundo.Janela.Regra, segundo.FatorR!.Value, segundo.Aliquota.Anexo, segundo.Das));

        // 5º mês: os meses antecedentes ao mês anterior, de 01 a 03/2027.
        var quinto = RegraSimples.Janela(new DateOnly(2027, 5, 1), new DateOnly(2027, 1, 1));
        Assert.Equal((RegraReceitaSimples.MediaInicio, 3), (quinto.Regra, quinto.Meses.Count));
        Assert.Equal(RegraReceitaSimples.DozeMeses, RegraSimples.Janela(new DateOnly(2028, 2, 1), new DateOnly(2027, 1, 1)).Regra);
    }

    [Fact]
    public void Anexo_iv_estima_a_cpp_fora_do_das()
    {
        var simples = RegraSimples.Calcular(Entrada(new DateOnly(2026, 10, 1), Meses(new DateOnly(2025, 10, 1), 12, 10_000m, 3_000m), 10_000m,
            AtividadeSimples.ServicosAnexoIV, remuneracoes: 10_000m)).Sucesso();

        Assert.Equal((AnexoSimples.IV, 450m, 2_000m), (simples.Aliquota.Anexo, simples.Das, simples.CppForaDoDas)); // 1ª faixa: 4,5%; CPP 20%.
        Assert.Null(simples.FatorR);
    }

    [Fact]
    public void Meses_faltantes_e_receita_acima_do_limite_sao_recusados()
    {
        var periodo = new DateOnly(2026, 10, 1);
        Assert.Contains("09/2026", RegraSimples.Calcular(Entrada(periodo, Meses(new DateOnly(2025, 10, 1), 11, 1_000m, 0m))).Falha().Mensagem);
        Assert.Contains("passa do limite", RegraSimples.Calcular(Entrada(periodo, Meses(new DateOnly(2025, 10, 1), 12, 401_000m, 0m))).Falha().Mensagem);
    }

    [Fact]
    public void Formulario_gera_os_meses_da_regra_e_preserva_os_valores()
    {
        var periodo = new CampoTextoViewModel("Período de apuração", TipoCampo.Competencia, "10/2026");
        var inicio = new CampoTextoViewModel("Início", TipoCampo.Competencia, "", permiteVazio: true);
        var campo = new CampoMesesSimplesViewModel(periodo, inicio) { ReceitaMensal = "20.000,00", FolhaMensal = "5.000,00" };
        Assert.True(campo.Gerar());
        Assert.Equal(("10/2025", "09/2026", 12), (campo.Meses[0].Competencia, campo.Meses[^1].Competencia, campo.Meses.Count));
        campo.Meses[^1].Receita = "25.000,00";

        periodo.Valor = "11/2026";
        Assert.True(campo.Gerar());
        Assert.Equal("25.000,00", campo.Meses.Single(linha => linha.Competencia == "09/2026").Receita);
        Assert.Equal("10/2026", campo.Meses[^1].Competencia);

        var reaberto = new CampoMesesSimplesViewModel(periodo, inicio);
        Assert.True(reaberto.Importar(campo.Exportar()));
        Assert.Equal(campo.Ler().Sucesso(), reaberto.Ler().Sucesso());
        Assert.False(reaberto.Importar("{inválido"));
    }
}
