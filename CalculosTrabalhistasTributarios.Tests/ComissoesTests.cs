using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.UseCases;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Tests.Referencia;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class ComissoesTests
{
    private static readonly DateOnly Outubro2026 = new(2026, 10, 1);

    private static decimal Valor(IEnumerable<VerbaDto> verbas, string descricao) => verbas.Single(verba => verba.Descricao.StartsWith(descricao, StringComparison.Ordinal)).Valor;

    [Fact]
    public void Calendario_conta_sabados_como_uteis_e_feriados_fora_do_domingo()
    {
        // Outubro de 2026: 31 dias, 4 domingos e 1 feriado em dia útil.
        var resultado = CalculadoraComissoes.Calcular(new ComissoesInformadas(Outubro2026, 2600m, false, 1, 0)).Sucesso();

        Assert.Equal(26, resultado.DiasUteis);
        Assert.Equal(5, resultado.DiasDescanso);
        Assert.Equal(500m, resultado.Dsr);
        Assert.Equal(3100m, resultado.Total);
        Assert.False(resultado.DiasInformados);
    }

    [Fact]
    public void Total_que_ja_inclui_dsr_e_separado_sem_aumentar_o_pagamento()
    {
        var resultado = CalculadoraComissoes.Calcular(new ComissoesInformadas(Outubro2026, 3100m, true, 1, 0)).Sucesso();

        Assert.Equal(2600m, resultado.Comissoes);
        Assert.Equal(500m, resultado.Dsr);
        Assert.Equal(3100m, resultado.Total);

        var centavos = CalculadoraComissoes.Calcular(new ComissoesInformadas(Outubro2026, 100m, true, 1, 0)).Sucesso();
        Assert.Equal(83.87m, centavos.Comissoes);
        Assert.Equal(16.13m, centavos.Dsr);
        Assert.Equal(100m, centavos.Total);
    }

    [Fact]
    public void Periodo_parcial_e_repouso_perdido_usam_somente_os_dias_pagos()
    {
        var entrada = new ComissoesInformadas(Outubro2026, 1000m, false, 0, 1, DiasUteis: 10, DiasDescanso: 2);
        var resultado = CalculadoraComissoes.Calcular(entrada).Sucesso();

        Assert.Equal(1, resultado.DescansosPagos);
        Assert.Equal(100m, resultado.Dsr);
        Assert.True(resultado.DiasInformados);
        Assert.Equal(0m, CalculadoraComissoes.Calcular(entrada with { DescansosPerdidos = 2 }).Sucesso().Dsr);
    }

    [Fact]
    public void Fevereiro_bissexto_e_arredondamento_sao_considerados()
    {
        // Fevereiro de 2024: 29 dias, quatro domingos e um feriado fora do domingo.
        var resultado = CalculadoraComissoes.Calcular(new ComissoesInformadas(new DateOnly(2024, 2, 1), 1000m, false, 1, 0)).Sucesso();

        Assert.Equal(24, resultado.DiasUteis);
        Assert.Equal(5, resultado.DiasDescanso);
        Assert.Equal(208.33m, resultado.Dsr);
    }

    [Fact]
    public void Dados_inconsistentes_sao_rejeitados_sem_divisao_por_zero()
    {
        var baseEntrada = new ComissoesInformadas(Outubro2026, 1000m, false, 0, 0);
        Assert.True(CalculadoraComissoes.Calcular(baseEntrada with { Valor = -1m }).Falhou);
        Assert.True(CalculadoraComissoes.Calcular(baseEntrada with { Feriados = -1 }).Falhou);
        Assert.True(CalculadoraComissoes.Calcular(baseEntrada with { Feriados = 31 }).Falhou);
        Assert.True(CalculadoraComissoes.Calcular(baseEntrada with { DescansosPerdidos = 5 }).Falhou);
        Assert.True(CalculadoraComissoes.Calcular(baseEntrada with { DiasUteis = 10 }).Falhou);
        Assert.True(CalculadoraComissoes.Calcular(baseEntrada with { DiasUteis = 0, DiasDescanso = 2 }).Falhou);
        Assert.True(CalculadoraComissoes.Calcular(baseEntrada with { DiasUteis = 30, DiasDescanso = 2 }).Falhou);
        Assert.True(CalculadoraComissoes.Calcular(baseEntrada with { DiasUteis = 10, DiasDescanso = 2, DescansosPerdidos = 3 }).Falhou);
    }

    [Fact]
    public async Task Simulacao_calcula_tributos_sobre_salario_comissoes_e_dsr()
    {
        var resultado = (await new SimularComissoesUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(
            new SimularComissoesRequest(Outubro2026, 3000m, 2600m, false, 1, 0, 0), default)).Sucesso();
        var modelo = ModeloTributario.Calcular(Outubro2026, 6100m, 0);
        var semComissoes = ModeloTributario.Calcular(Outubro2026, 3000m, 0);

        Assert.Equal(2600m, Valor(resultado.Proventos, "Comissões"));
        Assert.Equal(500m, Valor(resultado.Proventos, "DSR sobre comissões"));
        Assert.Equal(modelo.Inss, Valor(resultado.Descontos, "INSS"));
        Assert.Equal(modelo.IrrfRetido, Valor(resultado.Descontos, "IRRF"));
        Assert.Equal(488m, Valor(resultado.Informativos, "FGTS"));
        Assert.Equal(6100m - modelo.Inss - modelo.IrrfRetido, resultado.Resultado);
        var impacto = 3100m - (modelo.Inss - semComissoes.Inss) - (modelo.IrrfRetido - semComissoes.IrrfRetido);
        Assert.Equal(impacto.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), resultado.Destaques[2].Valor);
    }

    [Fact]
    public async Task Comissionista_puro_recebe_complemento_ate_o_salario_minimo()
    {
        var resultado = (await new SimularComissoesUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(
            new SimularComissoesRequest(Outubro2026, 0m, 1000m, false, 0, 0, 0), default)).Sucesso();

        Assert.Equal(1000m, Valor(resultado.Proventos, "Comissões"));
        Assert.Equal(148.15m, Valor(resultado.Proventos, "DSR sobre comissões"));
        Assert.Equal(472.85m, Valor(resultado.Proventos, "Complemento da garantia mínima"));
        Assert.Equal(1621m, resultado.TotalProventos);
        Assert.Equal(ModeloTributario.Calcular(Outubro2026, 1621m, 0).Inss, Valor(resultado.Descontos, "INSS"));
        Assert.Equal(129.68m, Valor(resultado.Informativos, "FGTS"));
        Assert.Equal(0m.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), resultado.Destaques[2].Valor);
    }

    [Fact]
    public async Task Dias_informados_exigem_garantia_do_periodo_e_mes_inteiro_nao_aceita_piso_abaixo_do_minimo()
    {
        var simulador = new SimularComissoesUseCase(await Ambiente.ConsultaAsync());
        var pedido = new SimularComissoesRequest(Outubro2026, 0m, 1000m, false, 0, 0, 0, 10, 2);

        Assert.True((await simulador.ExecutarAsync(pedido, default)).Falhou);
        Assert.True((await simulador.ExecutarAsync(pedido with { DiasUteis = null, DiasDescanso = null, PisoGarantido = 1000m }, default)).Falhou);
        Assert.True((await simulador.ExecutarAsync(pedido with { PisoGarantido = 1000m }, default)).Sucesso().TotalProventos > 1000m);
    }

    [Fact]
    public async Task Holerite_separa_dsr_das_comissoes_e_das_horas_extras()
    {
        var pedido = new SimularHoleriteRequest(Outubro2026, 3000m, GrauInsalubridade.Nenhum, false, 0m,
            220m, 10m, 50m, 0m, 100m, 0m, 20m, 1, 0, 0, 0m, 0, 0, null, 0m, 0m, 0m, Comissoes: 2600m);
        var resultado = (await new SimularHoleriteUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(pedido, default)).Sucesso();

        Assert.Equal(500m, Valor(resultado.Proventos, "DSR sobre comissões"));
        Assert.Contains(resultado.Proventos, verba => verba.Descricao.StartsWith("DSR sobre horas", StringComparison.Ordinal));
        Assert.Contains(resultado.Memoria, grupo => grupo.Titulo == "Comissões e DSR");
        var baseTributavel = resultado.Proventos.Sum(verba => verba.Valor);
        var modelo = ModeloTributario.Calcular(Outubro2026, baseTributavel, 0);
        Assert.Equal(modelo.Inss, Valor(resultado.Descontos, "INSS"));
        Assert.Equal(modelo.IrrfRetido, Valor(resultado.Descontos, "IRRF"));
        Assert.Equal(ModeloTributario.Arredondar(baseTributavel * .08m), Valor(resultado.Informativos, "FGTS"));
    }

    [Fact]
    public async Task Holerite_com_valor_que_ja_inclui_dsr_nao_duplica_comissoes()
    {
        var pedido = new SimularHoleriteRequest(Outubro2026, 3000m, GrauInsalubridade.Nenhum, false, 0m,
            220m, 0m, 50m, 0m, 100m, 0m, 20m, 1, 0, 0, 0m, 0, 0, null, 0m, 0m, 0m,
            Comissoes: 3100m, ComissoesIncluemDsr: true);
        var resultado = (await new SimularHoleriteUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(pedido, default)).Sucesso();

        Assert.Equal(2600m, Valor(resultado.Proventos, "Comissões"));
        Assert.Equal(500m, Valor(resultado.Proventos, "DSR sobre comissões"));
        Assert.Equal(6100m, resultado.TotalProventos);
    }

    [Fact]
    public async Task Holerite_aceita_comissionista_puro_e_aplica_garantia_minima()
    {
        var pedido = new SimularHoleriteRequest(Outubro2026, 0m, GrauInsalubridade.Nenhum, false, 0m,
            220m, 0m, 50m, 0m, 100m, 0m, 20m, 0, 0, 0, 0m, 0, 0, null, 0m, 0m, 0m, Comissoes: 1000m);
        var simulador = new SimularHoleriteUseCase(await Ambiente.ConsultaAsync());
        var resultado = (await simulador.ExecutarAsync(pedido, default)).Sucesso();

        Assert.DoesNotContain(resultado.Proventos, verba => verba.Descricao == "Salário");
        Assert.Equal(1621m, resultado.TotalProventos);
        Assert.Equal(472.85m, Valor(resultado.Proventos, "Complemento da garantia mínima"));
        Assert.True((await simulador.ExecutarAsync(pedido with { Comissoes = 0m }, default)).Falhou);
        Assert.True((await simulador.ExecutarAsync(pedido with { Salario = -1m }, default)).Falhou);
        Assert.True((await simulador.ExecutarAsync(pedido with { HorasFaixa1 = 2m }, default)).Falhou);
    }

    [Fact]
    public async Task Holerite_reduz_dsr_das_comissoes_por_repouso_perdido_sem_reduzir_a_base_duas_vezes()
    {
        var pedido = new SimularHoleriteRequest(Outubro2026, 3000m, GrauInsalubridade.Nenhum, false, 0m,
            220m, 0m, 50m, 0m, 100m, 0m, 20m, 1, 1, 1, 0m, 0, 0, null, 0m, 0m, 0m, Comissoes: 2600m);
        var resultado = (await new SimularHoleriteUseCase(await Ambiente.ConsultaAsync()).ExecutarAsync(pedido, default)).Sucesso();

        Assert.Equal(400m, Valor(resultado.Proventos, "DSR sobre comissões"));
        Assert.Equal(100m, Valor(resultado.Descontos, "DSR perdido"));
        Assert.Equal(100m, Valor(resultado.Descontos, "Faltas"));
        Assert.Equal(5800m, Valor(resultado.Informativos, "Base do INSS e do FGTS"));
    }
}
