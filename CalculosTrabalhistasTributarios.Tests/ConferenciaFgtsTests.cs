using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Conferência do FGTS por competência (Lei 8.036/1990, art. 15; LC 150/2015, art. 34), com valores à mão.</summary>
public class ConferenciaFgtsTests
{
    private static readonly DateOnly Desligamento = new(2024, 4, 15);

    private static LancamentoFgts[] Lancamentos() =>
    [
        new(new DateOnly(2024, 1, 1), TipoCompetenciaFgts.Mensal, 3_000m, 240m),
        new(new DateOnly(2024, 2, 1), TipoCompetenciaFgts.Mensal, 3_000m, 240m),
        new(new DateOnly(2024, 3, 1), TipoCompetenciaFgts.Mensal, 3_000m, 200m),
        new(new DateOnly(2024, 4, 1), TipoCompetenciaFgts.Rescisoria, 5_000m, 0m)
    ];

    [Fact]
    public void Devido_diferencas_e_vencimentos_seguem_o_criterio_de_cada_epoca()
    {
        var f = ConferenciaFgts.Calcular(CategoriaFgts.Empregado, Lancamentos(), Desligamento).Sucesso();

        Assert.Equal([240m, 240m, 240m, 400m], f.Linhas.Select(linha => linha.Devido));
        Assert.Equal((40m, 400m), (f.Falta(f.Mensais), f.Falta(f.Rescisorias)));
        // Até 02/2024, dia 7; desde 03/2024, dia 20 (20/04/2024 é sábado: antecipa para 19/04); rescisório, 10 dias após.
        Assert.Equal([new DateOnly(2024, 2, 7), new DateOnly(2024, 3, 7), new DateOnly(2024, 4, 19), new DateOnly(2024, 4, 25)], f.Linhas.Select(linha => linha.Vencimento!.Value));
    }

    [Fact]
    public void Aprendiz_tem_2_por_cento_e_domestico_soma_a_compensatoria_a_parte()
    {
        var mes = new LancamentoFgts(new DateOnly(2025, 5, 1), TipoCompetenciaFgts.Mensal, 2_000m, 0m);
        Assert.Equal(40m, ConferenciaFgts.Calcular(CategoriaFgts.Aprendiz, [mes], null).Sucesso().Linhas[0].Devido);
        var domestico = ConferenciaFgts.Calcular(CategoriaFgts.Domestico, [mes], null).Sucesso();
        Assert.Equal((160m, 64m), (domestico.Linhas[0].Devido, domestico.Compensatoria));
    }

    [Fact]
    public void Competencias_inconsistentes_com_o_desligamento_sao_recusadas()
    {
        var abril = new DateOnly(2024, 4, 1);
        Assert.Contains("lance-a como rescisória", ConferenciaFgts.Calcular(CategoriaFgts.Empregado, [new(abril, TipoCompetenciaFgts.Mensal, 1m, 0m)], Desligamento).Falha().Mensagem);
        Assert.Contains("exige a data de desligamento", ConferenciaFgts.Calcular(CategoriaFgts.Empregado, [new(abril, TipoCompetenciaFgts.Rescisoria, 1m, 0m)], null).Falha().Mensagem);
        Assert.Contains("mais de uma vez", ConferenciaFgts.Calcular(CategoriaFgts.Empregado, [new(abril, TipoCompetenciaFgts.Mensal, 1m, 0m), new(abril, TipoCompetenciaFgts.Mensal, 1m, 0m)], null).Falha().Mensagem);
    }

    [Fact]
    public async Task Demonstrativo_separa_mensal_e_rescisorio_e_leva_os_devidos_a_rescisao()
    {
        var d = (await new SimularConferenciaFgtsUseCase().ExecutarAsync(new SimularConferenciaFgtsRequest(CategoriaFgts.Empregado, Lancamentos(), Desligamento), default)).Sucesso();

        Assert.Equal(440m, d.Resultado); // 1.120 devidos - 680 informados.
        Assert.Equal(176m, d.Informativos.Single(verba => verba.Descricao.StartsWith("Reflexo")).Valor); // (40 + 400) × 40%.
        var transferencia = Assert.IsType<DepositosFgtsRescisaoDto>(d.DepositosFgts);
        Assert.Equal(3, transferencia.Depositos.Count);
        Assert.All(transferencia.Depositos, deposito => Assert.Equal(240m, deposito.Valor));

        var campoRescisao = new CampoDepositosFgtsViewModel(new("Data de admissão", TipoCampo.Data, "01/01/2023"), new("Data de desligamento", TipoCampo.Data, "15/04/2024"));
        Assert.True(campoRescisao.Importar(CampoDepositosFgtsViewModel.CriarImportacao(transferencia.Depositos)));
        Assert.Equal(transferencia.Depositos, campoRescisao.Ler().Sucesso());
    }

    [Fact]
    public void Formulario_gera_competencias_preserva_valores_e_a_rescisoria()
    {
        var campo = new CampoConferenciaFgtsViewModel { Inicio = "01/2024", Fim = "03/2024", RemuneracaoMensal = "3.000,00", DepositoMensal = "240,00" };
        Assert.True(campo.Gerar());
        campo.AdicionarRescisoriaCommand.Execute(null);
        campo.Linhas[^1].Competencia = "04/2024";
        campo.Linhas[1].Deposito = "200,00";
        campo.Fim = "02/2024";
        Assert.False(campo.Gerar()); // Primeiro clique só avisa a remoção.
        Assert.True(campo.Gerar());
        Assert.Equal(["01/2024", "02/2024", "04/2024"], campo.Linhas.Select(linha => linha.Competencia));
        Assert.Equal("200,00", campo.Linhas[1].Deposito);

        var reaberto = new CampoConferenciaFgtsViewModel();
        Assert.True(reaberto.Importar(campo.Exportar()));
        Assert.Equal(campo.Ler().Sucesso(), reaberto.Ler().Sucesso());
        Assert.False(reaberto.Importar("{inválido"));
    }
}
