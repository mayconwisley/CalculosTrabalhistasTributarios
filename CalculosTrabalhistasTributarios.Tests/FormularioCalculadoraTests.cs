using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Erros mostrados nos campos, resultado desatualizado e aviso de dados não salvos da janela padrão.</summary>
public class FormularioCalculadoraTests
{
    [Fact]
    public void Leitor_marca_todos_os_campos_invalidos_e_guarda_o_primeiro_erro()
    {
        var salario = new CampoTextoViewModel("Salário", TipoCampo.Moeda, "3.5x0,00");
        var competencia = new CampoTextoViewModel("Competência", TipoCampo.Competencia, "13/2026");
        var dependentes = new CampoTextoViewModel("Dependentes", TipoCampo.Inteiro, "2");
        var leitor = new LeitorFormulario();

        leitor.Moeda(salario);
        leitor.Competencia(competencia);
        leitor.Inteiro(dependentes);

        Assert.Equal("Use números e vírgula, como 3.500,00.", salario.MensagemErro);
        Assert.Equal("Use o mês e o ano, mm/aaaa.", competencia.MensagemErro);
        Assert.False(dependentes.TemErro);
        Assert.Equal("O campo \"Salário\" está em formato inválido: use somente números e vírgula nos centavos, por exemplo 3.500,00.", leitor.Erro?.Mensagem);

        // Ao digitar de novo, o erro sai do campo até o próximo cálculo.
        salario.Valor = "3.500,00";
        Assert.False(salario.TemErro);
    }

    [Fact]
    public void Campos_invalidos_ficam_no_formulario_sem_caixa_de_mensagem()
    {
        var (janela, calculadora, notificador) = Criar();
        CampoViewModel? focado = null;
        janela.CampoComErro += campo => focado = campo;
        calculadora.Salario.Valor = "abc";
        calculadora.Competencia.Valor = "00/2026";

        janela.CalcularCommand.Execute(null);

        Assert.Equal("Corrija os 2 campos destacados: Salário (R$) e Competência.", janela.Aviso);
        Assert.Same(calculadora.Salario, focado);
        Assert.Empty(notificador.Mensagens);
        Assert.False(janela.TemResultado);

        // O aviso fica enquanto houver um campo apontado e sai quando o último é corrigido.
        calculadora.Salario.Valor = "3.000,00";
        Assert.True(janela.TemAviso);
        calculadora.Competencia.Valor = "03/2026";
        Assert.False(janela.TemAviso);
    }

    [Fact]
    public void Um_campo_invalido_mostra_a_mensagem_completa()
    {
        var (janela, calculadora, _) = Criar();
        calculadora.Salario.Valor = "abc";

        janela.CalcularCommand.Execute(null);

        Assert.Equal("O campo \"Salário (R$)\" está em formato inválido: use somente números e vírgula nos centavos, por exemplo 3.500,00.", janela.Aviso);
    }

    [Fact]
    public void Regra_que_cita_o_rotulo_destaca_o_campo()
    {
        var (janela, calculadora, _) = Criar();
        calculadora.Falha = Erro.Validacao("Salário: informe um valor maior que zero.");

        janela.CalcularCommand.Execute(null);

        // O rótulo "Salário (R$)" é citado sem a unidade entre parênteses.
        Assert.Equal("Confira este campo.", calculadora.Salario.MensagemErro);
        Assert.Equal("Salário: informe um valor maior que zero.", janela.Aviso);
    }

    [Fact]
    public void Regra_sem_campo_citado_fica_so_no_aviso()
    {
        var (janela, calculadora, _) = Criar();
        CampoViewModel? focado = null;
        janela.CampoComErro += campo => focado = campo;
        calculadora.Falha = Erro.Validacao("O período excede o prazo do regime escolhido.");

        janela.CalcularCommand.Execute(null);

        Assert.Equal("O período excede o prazo do regime escolhido.", janela.Aviso);
        Assert.DoesNotContain(calculadora.Campos, campo => campo.TemErro);
        Assert.Null(focado);
    }

    [Fact]
    public void Tabela_indisponivel_continua_em_mensagem()
    {
        var (janela, calculadora, notificador) = Criar();
        calculadora.Falha = Erro.Indisponivel("Não há tabela de INSS para 01/1990.");

        janela.CalcularCommand.Execute(null);

        Assert.False(janela.TemAviso);
        Assert.Equal(["Não há tabela de INSS para 01/1990."], notificador.Mensagens);
    }

    [Fact]
    public void Campo_oculto_perde_o_erro()
    {
        var (janela, calculadora, _) = Criar();
        calculadora.Salario.Valor = "abc";
        janela.CalcularCommand.Execute(null);

        calculadora.Salario.Visivel = false;

        Assert.False(calculadora.Salario.TemErro);
        Assert.False(janela.TemAviso);
    }

    [Fact]
    public void Resultado_fica_desatualizado_quando_o_formulario_muda()
    {
        var (janela, calculadora, _) = Criar();
        var apresentados = 0;
        janela.ResultadoApresentado += () => apresentados++;

        janela.CalcularCommand.Execute(null);
        Assert.True(janela.TemResultado);
        Assert.False(janela.ResultadoDesatualizado);
        Assert.Equal(1, apresentados);

        calculadora.Salario.Valor = "4.000,00";
        Assert.True(janela.ResultadoDesatualizado);
        calculadora.Opcao.Selecionada = calculadora.Opcao.Opcoes[1];
        Assert.True(janela.ResultadoDesatualizado);

        // Voltar aos dados do cálculo deixa o resultado válido de novo.
        calculadora.Salario.Valor = "3.000,00";
        calculadora.Opcao.Selecionada = calculadora.Opcao.Opcoes[0];
        Assert.False(janela.ResultadoDesatualizado);

        calculadora.Salario.Valor = "4.000,00";
        janela.CalcularCommand.Execute(null);
        Assert.False(janela.ResultadoDesatualizado);
        Assert.Equal(2, apresentados);
    }

    [Fact]
    public void Passar_pelo_campo_nao_altera_o_formulario()
    {
        var (janela, calculadora, _) = Criar();
        var adiantamento = new CampoTextoViewModel("Adiantamento", TipoCampo.Moeda, "0,00") { MensagemErro = "Confira este campo." };
        janela.CalcularCommand.Execute(null);

        // O foco limpa o zero e a saída formata o número: o valor é o mesmo.
        adiantamento.Valor = string.Empty;
        calculadora.Salario.Valor = "3000";
        Assert.True(adiantamento.TemErro);
        Assert.False(janela.ResultadoDesatualizado);

        adiantamento.Valor = "0,01";
        Assert.False(adiantamento.TemErro);
    }

    [Fact]
    public async Task Recalculo_do_historico_nao_rola_a_tela()
    {
        var (janela, _, _) = Criar();
        var apresentados = 0;
        janela.ResultadoApresentado += () => apresentados++;

        await janela.RecalcularAsync();

        Assert.True(janela.TemResultado);
        Assert.Equal(0, apresentados);
    }

    [Fact]
    public void Exportacao_recalcula_o_formulario_e_nao_grava_resultado_antigo_quando_falha()
    {
        var calculadora = new CalculadoraFalsa();
        var pdf = new RelatorioFalso();
        var excel = new PlanilhaFalsa();
        var notificador = new NotificadorFalso();
        var janela = new CalculadoraViewModel(TipoCalculadora.Holerite, calculadora, notificador, pdf, excel,
            new ArquivoDialogFalso(), new ContextoCompartilhado(), new HistoricoDaJanelaFactory(null!, null!, notificador));
        janela.CalcularCommand.Execute(null);

        calculadora.Salario.Valor = "4.000,00";
        janela.ExportarPdfCommand.Execute(null);
        Assert.Equal(4_000m, pdf.Ultimo?.Resultado);
        Assert.False(janela.ResultadoDesatualizado);

        calculadora.Salario.Valor = "5.000,00";
        janela.ExportarExcelCommand.Execute(null);
        Assert.Equal(5_000m, excel.Ultimo?.Resultado);

        calculadora.Salario.Valor = "inválido";
        janela.ExportarPdfCommand.Execute(null);
        janela.ExportarExcelCommand.Execute(null);
        Assert.Equal(4_000m, pdf.Ultimo?.Resultado);
        Assert.Equal(5_000m, excel.Ultimo?.Resultado);
        Assert.True(janela.ResultadoDesatualizado);
    }

    [Fact]
    public void Fechar_pelo_esc_so_pergunta_com_dados_nao_salvos()
    {
        var (janela, calculadora, notificador) = Criar();
        janela.MarcarComoSalvo();

        Assert.True(janela.ConfirmarFechamento());
        Assert.Equal(0, notificador.Perguntas);

        calculadora.Salario.Valor = "4.000,00";
        notificador.Resposta = false;
        Assert.False(janela.ConfirmarFechamento());
        notificador.Resposta = true;
        Assert.True(janela.ConfirmarFechamento());
        Assert.Equal(2, notificador.Perguntas);

        // Salvo no histórico, o formulário volta a fechar sem perguntar.
        janela.MarcarComoSalvo();
        Assert.True(janela.ConfirmarFechamento());
        Assert.Equal(2, notificador.Perguntas);
    }

    private static (CalculadoraViewModel Janela, CalculadoraFalsa Calculadora, NotificadorFalso Notificador) Criar()
    {
        var calculadora = new CalculadoraFalsa();
        var notificador = new NotificadorFalso();
        var janela = new CalculadoraViewModel(TipoCalculadora.Holerite, calculadora, notificador, null!, null!, null!, new ContextoCompartilhado(),
            new HistoricoDaJanelaFactory(null!, null!, notificador));
        return (janela, calculadora, notificador);
    }

    private sealed class CalculadoraFalsa : CalculadoraBase
    {
        public CampoTextoViewModel Salario { get; } = Moeda("Salário (R$)");
        public CampoTextoViewModel Competencia { get; } = Competencia();
        public CampoOpcaoViewModel Opcao { get; } = CampoOpcaoViewModel.SimNao("Adiantamento", false);
        public Erro? Falha { get; set; }

        public CalculadoraFalsa() => Salario.Valor = "3.000,00";

        public override string Titulo => "Teste";
        public override string Descricao => string.Empty;
        public override string InstrucaoInicial => string.Empty;
        public override IReadOnlyList<CampoViewModel> Campos => [Salario, Competencia, Opcao];
        public override string NomeArquivoPdf => "teste";

        public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
        {
            var leitor = new LeitorFormulario();
            var salario = leitor.Moeda(Salario);
            leitor.Competencia(Competencia);
            if (leitor.Erro is { } erro)
                return Task.FromResult<Result<DemonstrativoDto>>(erro);
            if (Falha is { } falha)
                return Task.FromResult<Result<DemonstrativoDto>>(falha);
            return Task.FromResult<Result<DemonstrativoDto>>(new DemonstrativoDto("Teste", "03/2026", [], [new VerbaDto("Salário", "30 dias", salario)], [], [], [], []));
        }
    }

    private sealed class NotificadorFalso : IUserNotifier
    {
        public List<string> Mensagens { get; } = [];
        public int Perguntas { get; private set; }
        public bool Resposta { get; set; }

        public void MostrarAviso(string mensagem, string titulo = "Dados inválidos") => Mensagens.Add(mensagem);
        public void MostrarErro(string mensagem, Exception exception) => throw exception;
        public void MostrarFalha(Erro erro, string contexto) => Mensagens.Add(erro.Mensagem);

        public bool Confirmar(string mensagem, string titulo)
        {
            Perguntas++;
            return Resposta;
        }
    }

    private sealed class ArquivoDialogFalso : IArquivoDialogService
    {
        public string SolicitarDestinoPdf(string nomeArquivoSugerido) => "teste.pdf";
        public string SolicitarDestinoPlanilha(string nomeArquivoSugerido) => "teste.xlsx";
    }

    private sealed class RelatorioFalso : IRelatorioPdfService
    {
        public DemonstrativoDto? Ultimo { get; private set; }
        public Task GerarDemonstrativoAsync(DemonstrativoDto demonstrativo, string caminhoArquivo, CancellationToken cancellationToken)
        { Ultimo = demonstrativo; return Task.CompletedTask; }
        public Task GerarRelatorioImpostoAsync(SimulacaoImpostoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task GerarRelatorioPensaoAsync(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada, bool incluirDetalhes, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task GerarRelatorioPensaoAtrasoAsync(SimulacaoPensaoAtrasoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task GerarRelatorioDebitoJudicialAsync(SimulacaoDebitoJudicialDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task GerarRelatorioEstabilidadeAsync(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class PlanilhaFalsa : IPlanilhaService
    {
        public DemonstrativoDto? Ultimo { get; private set; }
        public Task GerarDemonstrativoAsync(DemonstrativoDto demonstrativo, string caminhoArquivo, CancellationToken cancellationToken)
        { Ultimo = demonstrativo; return Task.CompletedTask; }
        public Task GerarImpostoAsync(SimulacaoImpostoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task GerarPensaoAsync(SimulacaoPensaoDto simulacao, EntradaPensaoDto entrada, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task GerarPensaoAtrasoAsync(SimulacaoPensaoAtrasoDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task GerarEstabilidadeAsync(SimulacaoEstabilidadeDto simulacao, EntradaEstabilidadeDto entrada, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task GerarJornadaAsync(SimulacaoJornadaDto apuracao, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task GerarDebitoJudicialAsync(SimulacaoDebitoJudicialDto simulacao, string caminhoArquivo, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
