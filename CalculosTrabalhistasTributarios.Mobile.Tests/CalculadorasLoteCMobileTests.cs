using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CalculadorasLoteCMobileTests
{
    [Fact]
    public async Task Conferencia_fgts_gera_competencias_preserva_valores_e_envia_rescisoria()
    {
        var captura = new Captura<SimularConferenciaFgtsRequest>();
        var vm = new ConferenciaFgtsMobileViewModel(captura, new PdfTeste(), NullLogger<ConferenciaFgtsMobileViewModel>.Instance);
        Opcao(vm, "categoria", 2);
        Alterar(vm, "desligamento", "15/03/2026");
        Alterar(vm, "inicio", "01/2026");
        Alterar(vm, "fim", "02/2026");
        Alterar(vm, "remuneracaoMensal", "3.000,00");
        Alterar(vm, "depositoMensal", "240,00");
        var grade = Assert.Single(vm.Grades);
        grade.GerarCommand!.Execute(null);
        Assert.Equal(new[] { "01/2026", "02/2026" }, grade.Linhas.Select(linha => linha.Campo("competencia").Valor));
        Assert.All(grade.Linhas, linha => Assert.Equal("3.000,00", linha.Campo("remuneracao").Valor));

        grade.Linhas[1].Campo("deposito").Valor = "200,00";
        Alterar(vm, "inicio", "12/2025");
        grade.GerarCommand.Execute(null);
        Assert.Equal(new[] { "12/2025", "01/2026", "02/2026" }, grade.Linhas.Select(linha => linha.Campo("competencia").Valor));
        Assert.Equal("200,00", grade.Linhas[2].Campo("deposito").Valor);
        Assert.Contains("preservados", grade.Aviso);

        var rescisoria = grade.AdicionarLinha(new Dictionary<string, string> { ["competencia"] = "03/2026", ["remuneracao"] = "1.500,00" });
        Assert.NotNull(rescisoria);
        rescisoria.Campo("tipo").Indice = 1;
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = Assert.IsType<SimularConferenciaFgtsRequest>(captura.Pedido);
        Assert.Equal(CategoriaFgts.Domestico, pedido.Categoria);
        Assert.Equal(new DateOnly(2026, 3, 15), pedido.Desligamento);
        Assert.Equal(4, pedido.Lancamentos.Count);
        Assert.Equal(new LancamentoFgts(new DateOnly(2026, 2, 1), TipoCompetenciaFgts.Mensal, 3000m, 200m), pedido.Lancamentos[2]);
        Assert.Equal(new LancamentoFgts(new DateOnly(2026, 3, 1), TipoCompetenciaFgts.Rescisoria, 1500m, 0m), pedido.Lancamentos[3]);

        grade.Linhas[1].Campo("remuneracao").Valor = "abc";
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.StartsWith("Competência 2: O campo \"Remuneração (R$)\"", vm.Mensagem);
    }

    [Fact]
    public async Task Gorjetas_envia_meses_regime_e_incidencias_da_dispensa()
    {
        var captura = new Captura<SimularGorjetasRequest>();
        var vm = new GorjetasMobileViewModel(captura, new PdfTeste(), NullLogger<GorjetasMobileViewModel>.Instance);
        Opcao(vm, "regime", 1);
        Alterar(vm, "retencao", "20");
        Opcao(vm, "situacao", 1);
        Alterar(vm, "inicio", "01/2026");
        Alterar(vm, "fim", "03/2026");
        Alterar(vm, "valorMensal", "1.200,00");
        var grade = Assert.Single(vm.Grades);
        grade.GerarCommand!.Execute(null);
        grade.Linhas[1].Campo("valor").Valor = "1.350,50";
        await vm.CalcularCommand.ExecuteAsync(null);
        var entrada = Assert.IsType<SimularGorjetasRequest>(captura.Pedido).Entrada;
        Assert.False(entrada.RegimeDiferenciado);
        Assert.Equal(20m, entrada.PercentualRetencao);
        Assert.False(entrada.FeriasGozadas);
        Assert.Null(entrada.Admissao);
        Assert.Equal(IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts | IncidenciasReflexo.MultaFgts, entrada.Incidencias);
        Assert.Equal(new[] { 1200m, 1350.50m, 1200m }, entrada.Meses.Select(mes => mes.Valor));
        Assert.Equal(new DateOnly(2026, 3, 1), entrada.Meses[2].Competencia);

        grade.Linhas[2].Campo("competencia").Valor = "01/2026";
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.Contains("01/2026 aparece mais de uma vez", vm.Mensagem);

        grade.Linhas[1].Campo("valor").Valor = "x";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.StartsWith("Mês 2: O campo \"Gorjetas (R$)\"", vm.Mensagem);
    }

    [Fact]
    public async Task Sobreaviso_respeita_habitualidade_e_converte_horas_de_relogio()
    {
        var captura = new Captura<SimularSobreavisoRequest>();
        var vm = new SobreavisoMobileViewModel(captura, new PdfTeste(), NullLogger<SobreavisoMobileViewModel>.Instance);
        Assert.True(Campo(vm, "dsr").Marcado);
        Assert.True(Campo(vm, "dsrNosReflexos").Visivel);
        Assert.False(Campo(vm, "diasAviso").Visivel);
        Campo(vm, "dsr").Marcado = false;
        Assert.False(Campo(vm, "dsrNosReflexos").Visivel);
        Campo(vm, "dsr").Marcado = true;
        Campo(vm, "dsrNosReflexos").Marcado = true;
        Opcao(vm, "situacao", 1);
        Assert.True(Campo(vm, "diasAviso").Visivel);
        Alterar(vm, "diasAviso", "36");
        Alterar(vm, "admissao", "10/02/2020");
        Alterar(vm, "divisor", "200");
        Alterar(vm, "inicio", "01/2026");
        Alterar(vm, "fim", "02/2026");
        Alterar(vm, "salarioMensal", "3.300,00");
        Alterar(vm, "sobreavisoMensal", "40,5");
        Alterar(vm, "prontidaoMensal", "12:15");
        var grade = Assert.Single(vm.Grades);
        grade.GerarCommand!.Execute(null);
        Assert.Equal("40:30", grade.Linhas[0].Campo("sobreaviso").Valor);
        grade.Linhas[1].Campo("feriados").Valor = "2";
        await vm.CalcularCommand.ExecuteAsync(null);
        var entrada = Assert.IsType<SimularSobreavisoRequest>(captura.Pedido).Entrada;
        Assert.True(entrada.Habitual);
        Assert.Equal(200m, entrada.Divisor);
        Assert.True(entrada.DsrRepercuteNasOutras);
        Assert.Equal(36, entrada.DiasAvisoPrevio);
        Assert.Equal(new DateOnly(2020, 2, 10), entrada.Admissao);
        Assert.Equal(IncidenciasReflexo.Dsr | IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.AvisoPrevio
            | IncidenciasReflexo.Fgts | IncidenciasReflexo.MultaFgts, entrada.Incidencias);
        Assert.Equal(new MesSobreavisoEsperado(3300m, 40.5m, 12.25m, 2), Resumo(entrada.Meses[1]));

        Opcao(vm, "habitualidade", 1);
        Assert.False(Campo(vm, "dsr").Visivel);
        Assert.False(Campo(vm, "admissao").Visivel);
        Assert.False(Campo(vm, "diasAviso").Visivel);
        await vm.CalcularCommand.ExecuteAsync(null);
        entrada = Assert.IsType<SimularSobreavisoRequest>(captura.Pedido).Entrada;
        Assert.False(entrada.Habitual);
        Assert.False(entrada.DsrRepercuteNasOutras);
        Assert.Equal(0, entrada.DiasAvisoPrevio);
        Assert.Null(entrada.Admissao);
        Assert.Equal(IncidenciasReflexo.Fgts | IncidenciasReflexo.MultaFgts, entrada.Incidencias);

        grade.Linhas[0].Campo("feriados").Valor = "-1";
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.StartsWith("Mês 1: O campo \"Feriados em dias úteis\" não aceita valor negativo", vm.Mensagem);
    }

    [Fact]
    public async Task Transferencia_envia_dias_por_mes_e_aviso_so_na_dispensa()
    {
        var captura = new Captura<SimularTransferenciaRequest>();
        var vm = new TransferenciaMobileViewModel(captura, new PdfTeste(), NullLogger<TransferenciaMobileViewModel>.Instance);
        Assert.False(Campo(vm, "diasAviso").Visivel);
        Alterar(vm, "percentual", "30");
        Alterar(vm, "despesas", "800,00");
        Alterar(vm, "inicio", "11/2025");
        Alterar(vm, "fim", "01/2026");
        Alterar(vm, "salarioMensal", "4.000,00");
        var grade = Assert.Single(vm.Grades);
        grade.GerarCommand!.Execute(null);
        Assert.All(grade.Linhas, linha => Assert.Equal("30", linha.Campo("dias").Valor));
        grade.Linhas[0].Campo("dias").Valor = "15";
        await vm.CalcularCommand.ExecuteAsync(null);
        var entrada = Assert.IsType<SimularTransferenciaRequest>(captura.Pedido).Entrada;
        Assert.Equal(30m, entrada.Percentual);
        Assert.Equal(800m, entrada.Despesas);
        Assert.Equal(0, entrada.DiasAvisoPrevio);
        Assert.True(entrada.FeriasGozadas);
        Assert.Equal(IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts, entrada.Incidencias);
        Assert.Equal(new[] { 15, 30, 30 }, entrada.Meses.Select(mes => mes.Dias));
        Assert.Equal(new DateOnly(2025, 11, 1), entrada.Meses[0].Competencia);
        Assert.Equal(4000m, entrada.Meses[2].Salario);

        Opcao(vm, "situacao", 1);
        Assert.True(Campo(vm, "diasAviso").Visivel);
        Alterar(vm, "diasAviso", "45");
        await vm.CalcularCommand.ExecuteAsync(null);
        entrada = Assert.IsType<SimularTransferenciaRequest>(captura.Pedido).Entrada;
        Assert.Equal(45, entrada.DiasAvisoPrevio);
        Assert.False(entrada.FeriasGozadas);
        Assert.Equal(IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts
            | IncidenciasReflexo.AvisoPrevio | IncidenciasReflexo.MultaFgts, entrada.Incidencias);

        grade.Linhas[1].Campo("dias").Valor = "1,5";
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.StartsWith("Mês 2: O campo \"Dias transferido\"", vm.Mensagem);
    }

    [Fact]
    public async Task Media_verbas_ajusta_divisor_so_na_primeira_geracao_e_envia_meses()
    {
        var captura = new Captura<SimularMediaVerbasVariaveisRequest>();
        var vm = new MediaVerbasVariaveisMobileViewModel(captura, new PdfTeste(), NullLogger<MediaVerbasVariaveisMobileViewModel>.Instance);
        Alterar(vm, "inicio", "01/2026");
        Alterar(vm, "fim", "06/2026");
        var grade = Assert.Single(vm.Grades);
        grade.GerarCommand!.Execute(null);
        Assert.Equal(6, grade.Linhas.Count);
        Assert.Equal("6", Campo(vm, "divisor").Valor);
        Assert.Contains("Confira o divisor", grade.Aviso);
        grade.Linhas[0].Campo("comissoes").Valor = "1.000,00";
        grade.Linhas[0].Campo("horasExtras").Valor = "250,40";
        Alterar(vm, "divisor", "12");
        Alterar(vm, "fim", "07/2026");
        grade.GerarCommand.Execute(null);
        Assert.Equal(7, grade.Linhas.Count);
        Assert.Equal("12", Campo(vm, "divisor").Valor);
        Assert.Equal("1.000,00", grade.Linhas[0].Campo("comissoes").Valor);

        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = Assert.IsType<SimularMediaVerbasVariaveisRequest>(captura.Pedido);
        Assert.Equal(12, pedido.Divisor);
        Assert.Equal(7, pedido.Meses.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), pedido.Meses[0].Competencia);
        Assert.Equal(1000m, pedido.Meses[0].Comissoes);
        Assert.Equal(250.40m, pedido.Meses[0].HorasExtras);
        Assert.Equal(new DateOnly(2026, 7, 1), pedido.Meses[6].Competencia);

        grade.Linhas[2].Campo("dsr").Valor = "abc";
        captura.Limpar();
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(captura.Pedido);
        Assert.StartsWith("Mês 3: O campo \"DSR (R$)\"", vm.Mensagem);
    }

    private sealed record MesSobreavisoEsperado(decimal Salario, decimal Sobreaviso, decimal Prontidao, int Feriados);

    private static MesSobreavisoEsperado Resumo(CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso.MesSobreaviso mes) =>
        new(mes.Salario, mes.HorasSobreaviso, mes.HorasProntidao, mes.Feriados);

    [Fact]
    public async Task Gorjetas_com_erro_em_linha_fora_da_pagina_exibe_a_pagina_da_linha()
    {
        var vm = new GorjetasMobileViewModel(new Captura<SimularGorjetasRequest>(), new PdfTeste(), NullLogger<GorjetasMobileViewModel>.Instance);
        Alterar(vm, "inicio", "01/2025");
        Alterar(vm, "fim", "08/2026");
        var grade = Assert.Single(vm.Grades);
        grade.GerarCommand!.Execute(null);
        grade.Linhas[19].Campo("valor").Valor = "abc";

        await vm.CalcularCommand.ExecuteAsync(null);

        Assert.StartsWith("Mês 20:", vm.Mensagem);
        Assert.Equal("13–20 de 20", grade.Pagina.Texto);
        Assert.Contains(grade.Linhas[19], grade.Pagina.Exibidas);
    }

    private static CampoMobile Campo(CalculadoraTrabalhistaViewModel vm, string chave) =>
        Assert.Single(vm.Campos, campo => campo.Chave == chave);

    private static void Alterar(CalculadoraTrabalhistaViewModel vm, string chave, string valor) => Campo(vm, chave).Valor = valor;

    private static void Opcao(CalculadoraTrabalhistaViewModel vm, string chave, int indice) => Campo(vm, chave).Indice = indice;

    private sealed class Captura<T> : ISimularDemonstrativoUseCase<T>
    {
        public T? Pedido { get; private set; }
        public DemonstrativoDto? Resposta { get; init; }
        public void Limpar() => Pedido = default;
        public Task<Result<DemonstrativoDto>> ExecutarAsync(T request, CancellationToken cancellationToken)
        {
            Pedido = request;
            return Task.FromResult<Result<DemonstrativoDto>>(Resposta is { } demonstrativo
                ? demonstrativo : Erro.Validacao("Capturado para teste."));
        }
    }

    private sealed class PdfTeste : ICompartilhamentoPdf
    {
        public Task CompartilharResumoAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
        public DemonstrativoDto? Ultimo { get; private set; }
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken)
        {
            Ultimo = demonstrativo;
            return Task.CompletedTask;
        }
    }
}
