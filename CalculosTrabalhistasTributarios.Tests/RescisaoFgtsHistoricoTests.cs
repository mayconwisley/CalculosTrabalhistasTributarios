using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Xunit;
using CalculadoraFgtsRescisao = CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao.CalculadoraRescisao;

namespace CalculosTrabalhistasTributarios.Tests;

public class RescisaoFgtsHistoricoTests
{
    private static readonly DepositoFgtsHistorico[] Historico =
    [
        new(new DateOnly(2026, 1, 1), 200m),
        new(new DateOnly(2026, 2, 1), 250m),
        new(new DateOnly(2026, 3, 1), 300m)
    ];

    private static ContratoRescindido Contrato(decimal saldo = 0m, IReadOnlyList<DepositoFgtsHistorico>? depositos = null) =>
        new(new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 15), MotivoRescisao.DispensaSemJustaCausa,
            CumprimentoAvisoPrevio.TrabalhadoOuDispensado, 3000m, 0m, 0, 0, saldo, 0m,
            null, null, null, 0m, 0, DepositosFgts: depositos);

    [Fact]
    public void Historico_completo_usa_depositos_anteriores_e_nao_duplica_o_mes_da_rescisao()
    {
        var resultado = CalculadoraFgtsRescisao.Calcular(Contrato(depositos: Historico)).Valor.Fgts;
        // Jan/fev/mar: 200 + 250 + 300 = 750. Abril: (1.500 de salário + 1.000 de 13º) × 8% = 200.
        Assert.Equal(750m, resultado.TotalDepositosHistoricos);
        Assert.Equal(750m, resultado.Saldo);
        Assert.Equal(200m, resultado.Deposito);
        Assert.Equal(380m, resultado.Multa);
        Assert.Equal(1330m, resultado.Saque);
        Assert.True(resultado.SaldoPorHistorico);
    }

    [Fact]
    public void Extrato_prevalece_e_historico_fica_para_conferencia()
    {
        var resultado = CalculadoraFgtsRescisao.Calcular(Contrato(900m, Historico)).Valor.Fgts;
        Assert.Equal(750m, resultado.TotalDepositosHistoricos);
        Assert.Equal(900m, resultado.Saldo);
        Assert.Equal(440m, resultado.Multa);
        Assert.False(resultado.SaldoPorHistorico);
    }

    [Fact]
    public void Historico_incompleto_sem_extrato_nao_subestima_a_multa()
    {
        var resultado = CalculadoraFgtsRescisao.Calcular(Contrato(depositos: Historico[..2]));
        Assert.True(resultado.Falhou);
        Assert.Contains("Complete os meses ausentes", resultado.Erro.Mensagem);
    }

    [Fact]
    public void Mes_duplicado_ou_mes_da_rescisao_e_rejeitado()
    {
        var duplicado = CalculadoraFgtsRescisao.Calcular(Contrato(depositos: [.. Historico, Historico[0]]));
        var mesAtual = CalculadoraFgtsRescisao.Calcular(Contrato(depositos: [.. Historico, new(new DateOnly(2026, 4, 1), 200m)]));
        Assert.True(duplicado.Falhou);
        Assert.Contains("duas vezes", duplicado.Erro.Mensagem);
        Assert.True(mesAtual.Falhou);
        Assert.Contains("retire o mês da rescisão", mesAtual.Erro.Mensagem);
    }

    [Fact]
    public async Task Demonstrativo_mostra_a_conferencia_e_a_memoria_mensal()
    {
        var caso = new SimularRescisaoUseCase(await Ambiente.ConsultaAsync());
        var pedido = new SimularRescisaoRequest(new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 15),
            MotivoRescisao.DispensaSemJustaCausa, CumprimentoAvisoPrevio.TrabalhadoOuDispensado,
            3000m, 0m, 0, 0, 900m, 0m, 0, DepositosFgts: Historico);
        var demonstrativo = (await caso.ExecutarAsync(pedido, default)).Valor;
        Assert.Equal(750m, demonstrativo.Informativos.Single(item => item.Descricao.StartsWith("Depósitos históricos")).Valor);
        Assert.Contains(demonstrativo.Memoria.SelectMany(grupo => grupo.Formulas), formula => formula.Formula.Contains("R$ 900,00 (saldo informado) - R$ 750,00"));
    }

    [Fact]
    public void Formulario_preserva_linhas_e_abre_historico_antigo()
    {
        var admissao = new CampoTextoViewModel("Data de admissão", TipoCampo.Data, "01/01/2026");
        var desligamento = new CampoTextoViewModel("Data de desligamento", TipoCampo.Data, "15/04/2026");
        var campo = new CampoDepositosFgtsViewModel(admissao, desligamento);
        campo.GerarCommand.Execute(null);
        campo.Depositos[0].Valor = "200,00";
        campo.GerarCommand.Execute(null);
        Assert.Equal("200,00", campo.Depositos[0].Valor);
        var reaberto = new CampoDepositosFgtsViewModel(admissao, desligamento);
        Assert.True(reaberto.Importar(campo.Exportar()));
        Assert.Equal(3, reaberto.Depositos.Count);
        Assert.Equal("200,00", reaberto.Depositos[0].Valor);
        Assert.False(reaberto.Importar("{invalido"));
        Assert.Equal(3, reaberto.Depositos.Count);
        Assert.Empty(new CampoDepositosFgtsViewModel(admissao, desligamento).Depositos);
    }

    [Fact]
    public void Historico_da_rescisao_reabre_depositos_e_formato_antigo()
    {
        var calculadora = new CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras.CalculadoraRescisao(null!);
        calculadora.ImportarCampos(new Dictionary<string, string>
        {
            ["Data de admissão"] = "01/01/2026", ["Data de desligamento"] = "15/04/2026",
            ["Depósitos históricos do FGTS"] = "[{\"Competencia\":\"01/2026\",\"Valor\":\"200,00\"}]"
        });
        var salvo = calculadora.ExportarCampos();
        var reaberta = new CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras.CalculadoraRescisao(null!);
        reaberta.ImportarCampos(salvo);
        Assert.Equal(salvo, reaberta.ExportarCampos());

        var antiga = new CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras.CalculadoraRescisao(null!);
        antiga.ImportarCampos(new Dictionary<string, string> { ["Saldo do FGTS"] = "900,00" });
        Assert.Equal("[]", antiga.ExportarCampos()["Depósitos históricos do FGTS"]);
    }
}
