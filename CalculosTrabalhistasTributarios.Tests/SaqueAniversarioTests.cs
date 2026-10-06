using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class SaqueAniversarioTests
{
    [Theory]
    [InlineData(0, 2250)]
    [InlineData(500, 1750)]
    [InlineData(1800, 450)]
    [InlineData(2250, 0)]
    [InlineData(2249.99, 0.01)]
    public async Task Antecipacao_reduz_o_disponivel_sem_alterar_a_faixa(decimal comprometida, decimal disponivel)
    {
        // R$ 8.000 x 20% + R$ 650 = R$ 2.250, inclusive para contratos antigos acima de R$ 500.
        var parcela = SaqueAniversario.Calcular(8000m, comprometida).Sucesso();
        Assert.Equal(4, parcela.Faixa);
        Assert.Equal(2250m, parcela.Valor);
        Assert.Equal(disponivel, parcela.Disponivel);
        var dto = await new SimularSaqueAniversarioUseCase()
            .ExecutarAsync(new(8000m, 11, comprometida), default).Sucesso();
        Assert.Equal(2250m, dto.TotalProventos);
        Assert.Equal(comprometida, dto.TotalDescontos);
        Assert.Equal(disponivel, dto.Resultado);
        Assert.Equal("R$ 5.750,00", dto.Destaques[1].Valor);
        Assert.Equal("Disponível estimado", dto.RotuloResultado);
        Assert.Contains(dto.Memoria.SelectMany(grupo => grupo.Formulas), linha => linha.Titulo == "Repasse ao banco");
    }

    [Theory]
    [InlineData(8000, -0.01)]
    [InlineData(8000, 2250.01)]
    [InlineData(8000, 0.001)]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(8000.001, 0)]
    [InlineData(1000000000001, 0)]
    public void Entradas_inconsistentes_sao_rejeitadas(decimal saldo, decimal comprometida) =>
        Assert.Equal(TipoErro.Validacao, SaqueAniversario.Calcular(saldo, comprometida).Falha().Tipo);

    [Fact]
    public void Valores_extremos_nao_causam_overflow()
    {
        Assert.True(SaqueAniversario.Calcular(decimal.MaxValue).Falhou);
        Assert.True(SaqueAniversario.Calcular(8000m, decimal.MaxValue).Falhou);
        Assert.Equal(50_000_002_900m, SaqueAniversario.Calcular(1_000_000_000_000m).Sucesso().Valor);
    }

    [Fact]
    public async Task Historico_novo_reabre_e_antigo_nao_herda_antecipacao()
    {
        var original = new CalculadoraSaqueAniversario(new SimularSaqueAniversarioUseCase());
        original.ImportarCampos(new Dictionary<string, string>
        {
            ["Saldo do FGTS"] = "8.000,00",
            ["Mês de aniversário"] = "Novembro",
            ["Parcela ao banco (R$)"] = "1.800,00"
        });
        var reaberto = new CalculadoraSaqueAniversario(new SimularSaqueAniversarioUseCase());
        var dados = DadosFormulario.DeJson(new DadosFormulario(original.ExportarCampos()).ParaJson());
        reaberto.ImportarCampos(dados.Campos);
        Assert.Equal(original.ExportarCampos(), reaberto.ExportarCampos());
        Assert.Equal(450m, (await reaberto.CalcularAsync(default)).Sucesso().Resultado);

        reaberto.ImportarCampos(new Dictionary<string, string>
        {
            ["Saldo do FGTS"] = "8.000,00",
            ["Mês de aniversário"] = "Novembro"
        });
        Assert.Equal(2250m, (await reaberto.CalcularAsync(default)).Sucesso().Resultado);
        Assert.Equal("0,00", reaberto.ExportarCampos()["Parcela ao banco (R$)"]);
    }
}
