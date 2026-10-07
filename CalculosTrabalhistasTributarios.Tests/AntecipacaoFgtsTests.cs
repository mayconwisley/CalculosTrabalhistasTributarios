using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class AntecipacaoFgtsTests
{
    private static EntradaAnaliseAntecipacaoFgts Entrada(ConfirmacaoAntecipacaoFgts cedido = ConfirmacaoAntecipacaoFgts.NaoInformado) =>
        new(new DateOnly(2026, 10, 7), ConfirmacaoAntecipacaoFgts.Sim, ConfirmacaoAntecipacaoFgts.Sim, cedido);

    [Fact]
    public void Saldo_livre_nao_comprova_aprovacao_sem_situacao_das_competencias()
    {
        var a = AnalisadorAntecipacaoFgts.Analisar(18000m, 10000m, 4, Entrada()).Sucesso();
        Assert.Equal(8000m, a.SaldoForaGarantia);
        Assert.Equal(new DateOnly(2027, 4, 1), a.ProximoAniversario);
        Assert.Equal("Não confirmada", a.Situacao);
        Assert.Contains(a.Motivos, motivo => motivo.Contains("não é limite de empréstimo"));
    }

    [Fact]
    public void Proximo_saque_cedido_em_contrato_novo_aponta_impedimento()
    {
        var a = AnalisadorAntecipacaoFgts.Analisar(18000m, 10000m, 4, Entrada(ConfirmacaoAntecipacaoFgts.Sim)).Sucesso();
        Assert.Equal("Impedimento informado", a.Situacao);
        Assert.Contains(a.Motivos, motivo => motivo.Contains("quitação"));
    }

    [Fact]
    public void Contrato_antigo_exige_conferencia_da_transicao()
    {
        var a = AnalisadorAntecipacaoFgts.Analisar(18000m, 10000m, 4,
            Entrada(ConfirmacaoAntecipacaoFgts.Sim) with { ContratoDesdeNovembro2025 = ConfirmacaoAntecipacaoFgts.Nao }).Sucesso();
        Assert.Equal("Não confirmada", a.Situacao);
    }

    [Theory]
    [InlineData(2026, 10, 31, 5)]
    [InlineData(2026, 11, 1, 3)]
    public void Limite_de_novos_saques_obedece_data_da_analise(int ano, int mes, int dia, int limite)
    {
        var a = AnalisadorAntecipacaoFgts.Analisar(18000m, 0m, 4,
            Entrada(ConfirmacaoAntecipacaoFgts.Nao) with { DataConsulta = new(ano, mes, dia) }).Sucesso();
        Assert.Equal(limite, a.LimiteSaquesAnuais);
        Assert.Equal("Sujeita à análise do banco", a.Situacao);
    }

    [Fact]
    public void Carencia_nao_cumprida_e_garantia_integral_sao_impedimentos()
    {
        Assert.Equal("Impedimento informado", AnalisadorAntecipacaoFgts.Analisar(18000m, 18000m, 4, Entrada()).Sucesso().Situacao);
        Assert.Equal("Impedimento informado", AnalisadorAntecipacaoFgts.Analisar(18000m, 0m, 4,
            Entrada() with { AdesaoHa90Dias = ConfirmacaoAntecipacaoFgts.Nao }).Sucesso().Situacao);
    }

    [Fact]
    public void Dados_invalidos_e_periodo_anterior_as_regras_sao_rejeitados()
    {
        Assert.True(AnalisadorAntecipacaoFgts.Analisar(100m, 101m, 4, Entrada()).Falhou);
        Assert.True(AnalisadorAntecipacaoFgts.Analisar(100m, 0m, 4, Entrada() with { DataConsulta = new DateOnly(2025, 10, 31) }).Falhou);
        Assert.True(AnalisadorAntecipacaoFgts.Analisar(100m, 0m, 4, Entrada() with { ProximoSaqueComprometido = (ConfirmacaoAntecipacaoFgts)99 }).Falhou);
    }

    [Fact]
    public async Task Demonstrativo_separa_analise_de_credito_do_saque_anual()
    {
        var dto = await new SimularSaqueAniversarioUseCase().ExecutarAsync(new(18000m, 4, 0m, 10000m, 0m, true, Entrada()), default).Sucesso();
        Assert.Equal("Nova antecipação", dto.Destaques[0].Rotulo);
        Assert.Equal("Não confirmada", dto.Destaques[0].Valor);
        Assert.Equal(3700m, dto.Resultado); // 18.000 × 10% + 1.900; não representa crédito liberado.
        Assert.Equal("Saque anual após parcela informada", dto.RotuloResultado);
        Assert.Contains(dto.Memoria, grupo => grupo.Titulo == "Análise de nova antecipação");
    }

    [Fact]
    public void Historico_preserva_confirmacoes_e_antigo_nao_herda_respostas()
    {
        var calc = new CalculadoraSaqueAniversario(null!);
        calc.ImportarCampos(new Dictionary<string, string> { ["Data da análise"] = "07/10/2026", ["Contrato desde 11/2025"] = "Sim", ["Próximo saque cedido"] = "Sim" });
        var copia = new CalculadoraSaqueAniversario(null!);
        copia.ImportarCampos(calc.ExportarCampos());
        Assert.Equal(calc.ExportarCampos(), copia.ExportarCampos());
        copia.ImportarCampos(new Dictionary<string, string> { ["Saldo do FGTS"] = "8.000,00" });
        Assert.Equal("Não informado", copia.ExportarCampos()["Próximo saque cedido"]);
    }
}
