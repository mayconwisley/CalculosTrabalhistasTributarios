using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class CalculadorasLoteGMobileTests
{
    [Fact]
    public async Task Debito_judicial_encaminha_parcelas_datas_e_acrescimos_conforme_a_natureza()
    {
        var simulador = new DebitoTeste { Resposta = SimulacaoDebito() };
        var vm = new DebitoJudicialMobileViewModel(simulador, new PdfTeste(), NullLogger<DebitoJudicialMobileViewModel>.Instance);
        Assert.True(Campo(vm, "ajuizamento").Visivel);
        Assert.False(Campo(vm, "citacao").Visivel);
        Assert.False(Campo(vm, "acrescimos").Visivel);

        Alterar(vm, "dataCalculo", "30/09/2026");
        Alterar(vm, "ajuizamento", "15/03/2023");
        Opcao(vm, "acrescimos", 1);
        var parcelas = Grade(vm, "parcelas");
        Celula(parcelas, 0, "vencimento").Valor = "05/02/2022";
        Celula(parcelas, 0, "valor").Valor = "3.500,00";
        var segunda = parcelas.AdicionarLinha();
        Assert.NotNull(segunda);
        segunda.Campo("vencimento").Valor = "05/01/2022";
        segunda.Campo("valor").Valor = "1.000,50";
        await vm.CalcularCommand.ExecuteAsync(null);
        var trabalhista = simulador.Pedido;
        Assert.NotNull(trabalhista);
        Assert.Equal(NaturezaDebito.Trabalhista, trabalhista.Natureza);
        Assert.Equal(new DateOnly(2026, 9, 30), trabalhista.DataCalculo);
        Assert.Equal(new DateOnly(2023, 3, 15), trabalhista.DataAjuizamento);
        Assert.Null(trabalhista.DataCitacao);
        // Multa e honorários só valem no débito cível, como no desktop.
        Assert.Equal(AcrescimosPenhora.Nenhum, trabalhista.Acrescimos);
        Assert.Equal(new[] { "Parcela 1", "Parcela 2" }, trabalhista.Parcelas.Select(parcela => parcela.Descricao));
        Assert.Equal(new[] { 3500m, 1000.5m }, trabalhista.Parcelas.Select(parcela => parcela.Valor));
        Assert.Equal(new DateOnly(2022, 1, 5), trabalhista.Parcelas[1].Vencimento);
        Assert.NotNull(vm.Demonstrativo);

        Opcao(vm, "natureza", 1);
        Assert.False(Campo(vm, "ajuizamento").Visivel);
        Assert.True(Campo(vm, "acrescimos").Visivel);
        Assert.False(Campo(vm, "citacao").Visivel);
        Opcao(vm, "inicioJuros", 1);
        Assert.True(Campo(vm, "citacao").Visivel);
        Alterar(vm, "citacao", "10/06/2023");
        Opcao(vm, "indice", 1);
        Celula(parcelas, 0, "descricao").Valor = "Aluguel de janeiro";
        await vm.CalcularCommand.ExecuteAsync(null);
        var civel = simulador.Pedido;
        Assert.NotNull(civel);
        Assert.Equal(NaturezaDebito.Civel, civel.Natureza);
        Assert.Null(civel.DataAjuizamento);
        Assert.Equal(InicioJurosCivel.Citacao, civel.InicioJuros);
        Assert.Equal(new DateOnly(2023, 6, 10), civel.DataCitacao);
        Assert.Equal(IndiceCivelAnterior.Ipca, civel.IndiceAnterior);
        Assert.Equal(AcrescimosPenhora.MultaEHonorarios, civel.Acrescimos);
        Assert.Equal("Aluguel de janeiro", civel.Parcelas[0].Descricao);

        simulador.Limpar();
        segunda.Campo("vencimento").Valor = "31/02/2022";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(simulador.Pedido);
        Assert.StartsWith("Parcela 2:", vm.Mensagem);
        Assert.Null(vm.Demonstrativo);
    }

    [Fact]
    public async Task Debito_judicial_propaga_o_erro_do_caso_de_uso()
    {
        var vm = new DebitoJudicialMobileViewModel(new DebitoTeste(), new PdfTeste(), NullLogger<DebitoJudicialMobileViewModel>.Instance);
        var parcelas = Grade(vm, "parcelas");
        Celula(parcelas, 0, "vencimento").Valor = "05/02/2022";
        Celula(parcelas, 0, "valor").Valor = "100,00";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal("Capturado para teste.", vm.Mensagem);
        Assert.Null(vm.Demonstrativo);
    }

    [Fact]
    public void Demonstrativo_do_debito_reorganiza_os_valores_apurados()
    {
        var simulacao = SimulacaoDebito();
        var demonstrativo = DemonstrativoDebitoJudicial.Montar(simulacao);

        // O resultado é o total com acréscimos do DTO: parcelas 1.250,30 + 2.210,45, multa 346,08 e honorários 346,08.
        Assert.Equal(4152.91m, simulacao.TotalComAcrescimos);
        Assert.Equal(simulacao.TotalComAcrescimos, demonstrativo.Resultado);
        Assert.Empty(demonstrativo.Descontos);
        Assert.Equal(new[] { "Parcela 1", "Parcela 2", "Multa de 10% (CPC, art. 523)", "Honorários de 10% (CPC, art. 523)" },
            demonstrativo.Proventos.Select(verba => verba.Descricao));
        Assert.Equal("Total atualizado com acréscimos", demonstrativo.RotuloResultado);
        Assert.Equal("Atualização de débito cível", demonstrativo.Titulo);
        Assert.Equal("Cálculo em 30/09/2026", demonstrativo.Referencia);
        Assert.Equal(("Total atualizado", "R$ 4.152,91"), (demonstrativo.Destaques[0].Rotulo, Normalizar(demonstrativo.Destaques[0].Valor)));
        Assert.Equal("R$ 3.000,00", Normalizar(demonstrativo.Destaques[1].Valor));
        Assert.Equal("R$ 300,00", Normalizar(demonstrativo.Destaques[2].Valor));
        Assert.Equal("R$ 160,75", Normalizar(demonstrativo.Destaques[3].Valor));
        Assert.Equal("R$ 692,16", Normalizar(demonstrativo.Destaques[4].Valor));
        Assert.Equal(new[] { 3000m, 300m, 3300m, 160.75m }, demonstrativo.Informativos.Select(verba => verba.Valor));
        Assert.Equal(3, demonstrativo.Memoria.Count);
        Assert.Equal("Total: R$ 1.250,30", Normalizar(demonstrativo.Memoria[0].Destaque));
        Assert.Equal(new[] { "Critério A", "Observação B" }, demonstrativo.Observacoes);
    }

    [Fact]
    public void Demonstrativo_do_debito_sem_acrescimos_nao_inclui_multa()
    {
        var simulacao = SimulacaoDebito() with { Multa = 0m, Honorarios = 0m };
        var demonstrativo = DemonstrativoDebitoJudicial.Montar(simulacao);
        Assert.Equal(3460.75m, demonstrativo.Resultado);
        Assert.Equal(2, demonstrativo.Proventos.Count);
        Assert.Equal(2, demonstrativo.Memoria.Count);
        Assert.Equal("Não incluídos", demonstrativo.Destaques[4].Complemento);
        Assert.Equal("Total atualizado do débito", demonstrativo.RotuloResultado);
    }

    [Fact]
    public async Task Jornada_gera_os_dias_preserva_marcacoes_e_encaminha_o_pedido()
    {
        var apuracao = new JornadaTeste { Resposta = SimulacaoJornada() };
        var vm = new JornadaMobileViewModel(apuracao, new PdfTeste(), NullLogger<JornadaMobileViewModel>.Instance);
        Alterar(vm, "competencia", "11/2026");
        // O período que era o mês inteiro acompanha a competência.
        Assert.Equal("01/11/2026", Campo(vm, "inicio").Valor);
        Assert.Equal("30/11/2026", Campo(vm, "fim").Valor);
        Alterar(vm, "competencia", "10/2026");
        Assert.Equal("01/10/2026", Campo(vm, "inicio").Valor);
        Assert.Equal("31/10/2026", Campo(vm, "fim").Valor);
        Alterar(vm, "inicio", "05/10/2026");
        Alterar(vm, "fim", "11/10/2026");
        Opcao(vm, "noturno", 1);
        var dias = Grade(vm, "dias");
        Assert.True(dias.TemGerar);
        dias.GerarCommand!.Execute(null);
        Assert.Equal(7, dias.Linhas.Count);
        // 05/10/2026 é segunda-feira: sábado só com o primeiro período e domingo de descanso.
        Assert.Equal("8:00", Celula(dias, 0, "previsto").Valor);
        Assert.Equal("17:00", Celula(dias, 0, "saida2").Valor);
        Assert.Equal("4:00", Celula(dias, 5, "previsto").Valor);
        Assert.Equal("", Celula(dias, 5, "entrada2").Valor);
        Assert.Equal((int)TipoDia.Descanso, Celula(dias, 6, "tipo").Indice);
        Assert.NotNull(dias.Aviso);

        Celula(dias, 0, "saida2").Valor = "18:30";
        Celula(dias, 2, "tipo").Indice = (int)TipoDia.Feriado;
        dias.GerarCommand.Execute(null);
        Assert.Equal(7, dias.Linhas.Count);
        Assert.Equal("18:30", Celula(dias, 0, "saida2").Valor);

        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = apuracao.Pedido;
        Assert.NotNull(pedido);
        Assert.Equal(new DateOnly(2026, 10, 1), pedido.Competencia);
        Assert.Equal(TrabalhoNoturno.RuralLavoura, pedido.Noturno);
        Assert.False(pedido.Escala12x36);
        Assert.Equal(Enumerable.Range(5, 7).Select(dia => new DateOnly(2026, 10, dia)), pedido.Dias.Select(dia => dia.Data));
        Assert.Equal(new TimeOnly(18, 30), pedido.Dias[0].Periodos[1].Saida);
        Assert.Equal(480, pedido.Dias[0].MinutosPrevistos);
        Assert.Equal(TipoDia.Feriado, pedido.Dias[2].Tipo);
        Assert.Equal(240, pedido.Dias[5].MinutosPrevistos);
        Assert.Single(pedido.Dias[5].Periodos);
        Assert.Equal(TipoDia.Descanso, pedido.Dias[6].Tipo);
        Assert.Empty(pedido.Dias[6].Periodos);
        Assert.NotNull(vm.Demonstrativo);

        apuracao.Limpar();
        Celula(dias, 1, "entrada1").Valor = "25:00";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Null(apuracao.Pedido);
        Assert.StartsWith("Dia 2:", vm.Mensagem);
        Assert.Contains("entrada e a saída", vm.Mensagem);
    }

    [Fact]
    public async Task Jornada_por_escala_mostra_o_ciclo_e_gera_os_dias_ao_calcular()
    {
        var apuracao = new JornadaTeste();
        var vm = new JornadaMobileViewModel(apuracao, new PdfTeste(), NullLogger<JornadaMobileViewModel>.Instance);
        Assert.False(Campo(vm, "inicioEscala").Visivel);
        Assert.True(Campo(vm, "jornadaSabado").Visivel);
        Opcao(vm, "escala", 2);
        Assert.True(Campo(vm, "inicioEscala").Visivel);
        Assert.False(Campo(vm, "jornadaSabado").Visivel);
        Alterar(vm, "competencia", "10/2026");
        Alterar(vm, "inicio", "01/10/2026");
        Alterar(vm, "fim", "06/10/2026");
        Alterar(vm, "inicioEscala", "02/10/2026");
        Alterar(vm, "jornadaSemana", "11:00");

        // Sem dias gerados, Calcular gera o período e apura, como o Apurar do desktop.
        await vm.CalcularCommand.ExecuteAsync(null);
        var pedido = apuracao.Pedido;
        Assert.NotNull(pedido);
        Assert.True(pedido.Escala12x36);
        Assert.Equal(new[] { TipoDia.Descanso, TipoDia.Util, TipoDia.Descanso, TipoDia.Util, TipoDia.Descanso, TipoDia.Util }, pedido.Dias.Select(dia => dia.Tipo));
        Assert.Equal(660, pedido.Dias[1].MinutosPrevistos);
        Assert.Equal(2, pedido.Dias[1].Periodos.Count);
        Assert.Equal("Capturado para teste.", vm.Mensagem);

        Alterar(vm, "fim", "30/12/2026");
        Grade(vm, "dias").GerarCommand!.Execute(null);
        Assert.Contains("62", vm.Mensagem);
    }

    [Fact]
    public void Demonstrativo_da_jornada_mostra_os_totais_em_horas_sem_verbas()
    {
        var demonstrativo = DemonstrativoJornada.Montar(SimulacaoJornada(),
            [new MarcacaoDia(new DateOnly(2026, 10, 5), TipoDia.Util, 480, [new PeriodoTrabalhado(new TimeOnly(8, 0), new TimeOnly(12, 0)), new PeriodoTrabalhado(new TimeOnly(13, 0), new TimeOnly(18, 30))])]);

        Assert.False(demonstrativo.TemVerbas);
        Assert.Equal("Jornada pelo ponto", demonstrativo.Titulo);
        Assert.Equal("Competência 10/2026 • ponto de 05/10/2026 a 06/10/2026", demonstrativo.Referencia);
        Assert.Equal(("Horas trabalhadas", "9:30", "Previstas: 16:00"),
            (demonstrativo.Destaques[0].Rotulo, demonstrativo.Destaques[0].Valor, demonstrativo.Destaques[0].Complemento));
        Assert.Equal("1:30", demonstrativo.Destaques[1].Valor);
        Assert.Equal("1 dia", demonstrativo.Destaques[4].Valor);
        Assert.Equal("0:20 de atrasos; 1 DSR perdido(s)", demonstrativo.Destaques[4].Complemento);
        var totais = demonstrativo.Comparativo;
        Assert.NotNull(totais);
        Assert.Equal(new[] { "Total" }, totais.Colunas);
        Assert.Equal("1:30", Assert.Single(totais.Linhas, linha => linha.Descricao.StartsWith("Horas extras (faixa 1)")).Valores[0]);
        Assert.Equal("1", Assert.Single(totais.Linhas, linha => linha.Descricao == "Faltas (dias)").Valores[0]);
        var grupo = Assert.Single(demonstrativo.Memoria);
        Assert.Equal(2, grupo.Formulas.Count);
        Assert.Contains("08:00–12:00, 13:00–18:30", grupo.Formulas[0].Formula);
        Assert.Contains("extras 1:30", grupo.Formulas[0].Formula);
        Assert.Contains("falta", grupo.Formulas[1].Formula);
        Assert.Equal(new[] { "Critério J", "Observação J" }, demonstrativo.Observacoes);
    }

    private static SimulacaoDebitoJudicialDto SimulacaoDebito() => new(
        NaturezaDebito.Civel,
        new DateOnly(2026, 9, 30),
        [
            new ParcelaDebitoDto("Parcela 1", new DateOnly(2022, 1, 5), 1000m, 1.12m, 0m, 1120m, 11.6339m, 130.30m, 1250.30m),
            new ParcelaDebitoDto("Parcela 2", new DateOnly(2022, 2, 5), 2000m, 1.05m, 4.7619m, 2180m, 1.3968m, 30.45m, 2210.45m)
        ],
        ["Critério A"],
        ["Observação B"],
        Multa: 346.08m,
        Honorarios: 346.08m);

    private static SimulacaoJornadaDto SimulacaoJornada() => new(
        new DateOnly(2026, 10, 1),
        TrabalhoNoturno.Urbano,
        [
            new DiaJornadaDto(new DateOnly(2026, 10, 5), TipoDia.Util, 480, 570, 90, 0, 0, 0, false, 0, 0),
            new DiaJornadaDto(new DateOnly(2026, 10, 6), TipoDia.Util, 480, 0, 0, 0, 0, 0, true, 0, 0)
        ],
        new TotaisJornadaDto(Trabalhadas: 570, Previstas: 960, ExtrasFaixa1: 90, ExtrasFaixa2: 0, Noturnas: 0, ExtrasNoturnas: 0, Faltas: 1,
            DescansosPerdidos: 1, Faltantes: 20, IntervaloSuprimido: 0, InterjornadaSuprimida: 0, Feriados: 0),
        ["Critério J"],
        ["Observação J"]);

    /// <summary>A moeda pt-BR do .NET separa o símbolo com espaço não separável.</summary>
    private static string Normalizar(string texto) => texto.Replace(' ', ' ');

    private static CampoMobile Campo(CalculadoraTrabalhistaViewModel vm, string chave) => Assert.Single(vm.Campos, campo => campo.Chave == chave);

    private static void Alterar(CalculadoraTrabalhistaViewModel vm, string chave, string valor) => Campo(vm, chave).Valor = valor;

    private static void Opcao(CalculadoraTrabalhistaViewModel vm, string chave, int indice) => Campo(vm, chave).Indice = indice;

    private static GradeMobile Grade(CalculadoraTrabalhistaViewModel vm, string chave) => Assert.Single(vm.Grades, grade => grade.Chave == chave);

    private static CampoMobile Celula(GradeMobile grade, int linha, string chave) => grade.Linhas[linha].Campo(chave);

    private sealed class DebitoTeste : ISimularDebitoJudicialUseCase
    {
        public SimularDebitoJudicialRequest? Pedido { get; private set; }
        public SimulacaoDebitoJudicialDto? Resposta { get; init; }
        public void Limpar() => Pedido = null;
        public Task<Result<SimulacaoDebitoJudicialDto>> CalcularAsync(SimularDebitoJudicialRequest request, CancellationToken cancellationToken)
        {
            Pedido = request;
            return Task.FromResult<Result<SimulacaoDebitoJudicialDto>>(Resposta is { } simulacao ? simulacao : Erro.Validacao("Capturado para teste."));
        }
    }

    private sealed class JornadaTeste : IApurarJornadaUseCase
    {
        public ApurarJornadaRequest? Pedido { get; private set; }
        public SimulacaoJornadaDto? Resposta { get; init; }
        public void Limpar() => Pedido = null;
        public Result<SimulacaoJornadaDto> Apurar(ApurarJornadaRequest request)
        {
            Pedido = request;
            return Resposta is { } apuracao ? apuracao : Erro.Validacao("Capturado para teste.");
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
