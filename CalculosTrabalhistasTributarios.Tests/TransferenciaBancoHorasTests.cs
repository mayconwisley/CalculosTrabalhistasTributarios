using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Tests.Referencia;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class TransferenciaBancoHorasTests
{
    private static readonly DateOnly Fim = new(2026, 10, 31);

    private static async Task<QuitacaoBancoHoras> ApurarAsync(SituacaoBancoHoras situacao)
    {
        var resultado = (await new SimularBancoHorasUseCase().ExecutarAsync(new SimularBancoHorasRequest(
            new DateOnly(2026, 10, 1), Fim, RegimeBancoHoras.MesmoMes, situacao, 2200m, 220m, 50m,
            [
                new(new DateOnly(2026, 10, 1), TipoLancamentoBancoHoras.Credito, 90, "50%", 50m),
                new(new DateOnly(2026, 10, 2), TipoLancamentoBancoHoras.Credito, 60, "100%", 100m),
                new(new DateOnly(2026, 10, 3), TipoLancamentoBancoHoras.Compensacao, 60, "folga")
            ]), default)).Sucesso();
        Assert.Equal(27.50m, resultado.Resultado); // 30 min × R$ 10 × 150% + 60 min × R$ 10 × 200%.
        return Assert.IsType<QuitacaoBancoHoras>(resultado.QuitacaoBancoHoras);
    }

    [Fact]
    public async Task Fechamento_gera_duas_verbas_no_holerite_e_as_inclui_nas_tres_bases()
    {
        var quitacao = await ApurarAsync(SituacaoBancoHoras.Fechamento);
        var request = new SimularHoleriteRequest(new DateOnly(2026, 10, 1), 2200m, GrauInsalubridade.Nenhum,
            false, 0m, 220m, 0m, 50m, 0m, 100m, 0m, 20m, 0, 0, 0, 0m, 0, 0,
            null, 0m, 0m, 0m, QuitacaoBancoHoras: quitacao);
        var resultado = (await new SimularHoleriteUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(request, default)).Sucesso();
        var parcelas = resultado.Proventos.Where(p => p.Descricao.StartsWith("Quitação do banco de horas")).ToArray();
        Assert.Equal([("0:30", 7.50m), ("1:00", 20m)], parcelas.Select(p => (p.Referencia, p.Valor)));
        Assert.Equal(2227.50m, resultado.Informativos.Single(p => p.Descricao == "Base do INSS e do FGTS").Valor);
        Assert.Equal(ModeloTributario.Calcular(new DateOnly(2026, 10, 1), 2227.50m, 0).Inss,
            resultado.Descontos.Single(p => p.Descricao == "INSS").Valor);
        Assert.Equal(ModeloTributario.Arredondar(2227.50m * .08m),
            resultado.Informativos.Single(p => p.Descricao == "FGTS").Valor);
        Assert.Contains(resultado.Memoria, grupo => grupo.Titulo == "Quitação do banco de horas"
            && grupo.Formulas.Count(p => p.Titulo.StartsWith("Adicional")) == 2);
    }

    [Fact]
    public async Task Rescisao_recebe_parcelas_e_acrescenta_deposito_de_fgts_do_mes()
    {
        var quitacao = await ApurarAsync(SituacaoBancoHoras.Rescisao);
        var request = new SimularRescisaoRequest(new DateOnly(2025, 1, 1), Fim,
            MotivoRescisao.PedidoDeDemissao, CumprimentoAvisoPrevio.TrabalhadoOuDispensado,
            2200m, 0m, 0, 0, 0m, 0m, 0, QuitacaoBancoHoras: quitacao);
        var simulador = new SimularRescisaoUseCase(await Ambiente.ConsultaAsync());
        var comQuitacao = (await simulador.ExecutarAsync(request, default)).Sucesso();
        var semQuitacao = (await simulador.ExecutarAsync(request with { QuitacaoBancoHoras = null }, default)).Sucesso();
        Assert.Equal([7.50m, 20m], comQuitacao.Proventos.Where(p => p.Descricao.StartsWith("Quitação do banco de horas")).Select(p => p.Valor));
        Assert.Equal(27.50m, comQuitacao.TotalProventos - semQuitacao.TotalProventos);
        Assert.Equal(2.20m, comQuitacao.Informativos.Single(p => p.Descricao.StartsWith("Depósito do FGTS")).Valor
            - semQuitacao.Informativos.Single(p => p.Descricao.StartsWith("Depósito do FGTS")).Valor);
        Assert.Contains(comQuitacao.Memoria, grupo => grupo.Titulo == "Quitação do banco de horas");
    }

    [Fact]
    public async Task Transferencia_preenche_o_holerite_e_so_calcula_apos_a_conferencia()
    {
        var (destino, valores) = CampoQuitacaoBancoHorasViewModel.Transferencia(await ApurarAsync(SituacaoBancoHoras.Fechamento));
        Assert.Equal(TipoCalculadora.Holerite, destino);
        var holerite = new CalculadoraHolerite(new SimularHoleriteUseCase(await Ambiente.ConsultaAsync()));
        holerite.ImportarCampos(valores);

        var campos = holerite.ExportarCampos();
        Assert.Equal("10/2026", campos["Competência"]);
        Assert.Equal("2.200,00", campos["Salário"]);
        Assert.Equal("220,00", campos["Divisor de horas"]);
        Assert.Contains("Confira as incidências", (await holerite.CalcularAsync(default)).Falha().Mensagem);

        holerite.Campos.OfType<CampoQuitacaoBancoHorasViewModel>().Single().Confirmado = true;
        var resultado = (await holerite.CalcularAsync(default)).Sucesso();
        Assert.Equal([("Quitação do banco de horas (50%)", 7.50m), ("Quitação do banco de horas (100%)", 20m)],
            resultado.Proventos.Where(p => p.Descricao.StartsWith("Quitação do banco de horas")).Select(p => (p.Descricao, p.Valor)));
        Assert.Contains(resultado.Memoria, grupo => grupo.Titulo == "Quitação do banco de horas");
    }

    [Fact]
    public async Task Transferencia_preenche_a_rescisao_na_data_do_fim_do_ciclo()
    {
        var (destino, valores) = CampoQuitacaoBancoHorasViewModel.Transferencia(await ApurarAsync(SituacaoBancoHoras.Rescisao));
        Assert.Equal(TipoCalculadora.Rescisao, destino);
        var rescisao = new Presentation.ViewModels.Calculadoras.CalculadoraRescisao(new SimularRescisaoUseCase(await Ambiente.ConsultaAsync()));
        rescisao.ImportarCampos(new Dictionary<string, string>(valores) { ["Data de admissão"] = "01/01/2025" });
        Assert.Equal("31/10/2026", rescisao.ExportarCampos()["Data de desligamento"]);

        rescisao.Campos.OfType<CampoQuitacaoBancoHorasViewModel>().Single().Confirmado = true;
        var resultado = (await rescisao.CalcularAsync(default)).Sucesso();
        Assert.Equal([7.50m, 20m], resultado.Proventos.Where(p => p.Descricao.StartsWith("Quitação do banco de horas")).Select(p => p.Valor));
    }

    [Fact]
    public async Task Nao_aceita_destino_ou_valor_divergente()
    {
        var fechamento = await ApurarAsync(SituacaoBancoHoras.Fechamento);
        Assert.True(fechamento.Validar(SituacaoBancoHoras.Rescisao, Fim).Falhou);
        Assert.True(fechamento.Validar(SituacaoBancoHoras.Fechamento, new DateOnly(2026, 11, 1)).Falhou);
        var adulterada = fechamento with { Parcelas = [fechamento.Parcelas[0] with { Valor = 8m }] };
        Assert.True(adulterada.Validar(SituacaoBancoHoras.Fechamento, Fim).Falhou);
        Assert.True((fechamento with { Situacao = (SituacaoBancoHoras)42 }).Validar((SituacaoBancoHoras)42, Fim).Falhou);
    }

    [Fact]
    public async Task Formulario_exige_conferencia_e_preserva_parcelas_ao_reabrir()
    {
        var quitacao = await ApurarAsync(SituacaoBancoHoras.Fechamento);
        var campo = new CampoQuitacaoBancoHorasViewModel();
        Assert.True(campo.Importar(CampoQuitacaoBancoHorasViewModel.CriarImportacao(quitacao)));
        Assert.True(campo.Ler().Falhou);
        Assert.Equal(["50,00%", "100,00%"], campo.Parcelas.Select(p => p.Adicional));
        campo.Confirmado = true;
        Assert.Equal(27.50m, campo.Ler().Sucesso()?.Total);
        var reaberto = new CampoQuitacaoBancoHorasViewModel();
        Assert.True(reaberto.Importar(campo.Exportar()));
        Assert.True(reaberto.Confirmado);
        Assert.Equal(27.50m, reaberto.Ler().Sucesso()?.Total);
        Assert.False(reaberto.Importar("{json inválido"));
        Assert.Equal(27.50m, reaberto.Ler().Sucesso()?.Total);
        reaberto.DescartarCommand.Execute(null);
        Assert.Null(reaberto.Ler().Sucesso());
    }
}
