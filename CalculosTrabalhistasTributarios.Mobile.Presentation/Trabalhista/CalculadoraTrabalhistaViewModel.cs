using System.Globalization;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public abstract partial class CalculadoraTrabalhistaViewModel : ObservableObject, IFormularioEditavelMobile
{
    private readonly ICompartilhamentoPdf compartilhamento;
    private readonly ILogger logger;
    public event EventHandler? FormularioAlterado;
    protected CalculadoraTrabalhistaViewModel(ICompartilhamentoPdf compartilhamento, ILogger logger)
    {
        this.compartilhamento = compartilhamento;
        this.logger = logger;
        _formulario.Alterado += (_, _) => NotificarEdicao();
        AcompanharRemocao(_formulario.RemocaoLinhas);
    }
    private void NotificarEdicao() => FormularioAlterado?.Invoke(this, EventArgs.Empty);
    protected static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly EstadoFormularioTrabalhista _formulario = new();
    private Dictionary<string, CampoMobile> _campos => _formulario.Campos;
    private IReadOnlyList<SecaoCamposMobile>? _secoes;
    [ObservableProperty] private CampoMobile? _primeiroCampoComErro;
    /// <summary>Grade apontada por uma regra sem campo citado; a página a traz à vista.</summary>
    [ObservableProperty] private GradeMobile? _gradeComErro;
    // O aviso resume campos destacados: sai quando todos forem corrigidos. Um aviso sem campo sai na próxima edição.
    private bool _avisoDosCampos;

    public IReadOnlyList<SecaoCamposMobile> Secoes => _secoes ??= AgruparCampos();
    private IReadOnlyList<SecaoCamposMobile> AgruparCampos()
    {
        var secoes = new List<SecaoCamposMobile>();
        var campos = new List<CampoMobile>();
        var titulo = "Dados do cálculo";
        foreach (var campo in _campos.Values)
        {
            if (!campo.EhSecao) { campos.Add(campo); continue; }
            if (campos.Count > 0) secoes.Add(new(titulo, campos.ToArray()));
            campos.Clear();
            titulo = campo.Rotulo;
        }
        if (campos.Count > 0) secoes.Add(new(titulo, campos.ToArray()));
        return secoes;
    }

    private IEnumerable<CampoMobile> TodosCampos => _campos.Values.Concat(Grades.SelectMany(g => g.Linhas).SelectMany(l => l.Campos));

    /// <summary>
    /// Mesmo padrão do aplicativo Windows: antes do cálculo, todos os campos à vista têm o formato conferido; cada campo errado
    /// recebe abaixo dele o que corrigir e o aviso junto de Calcular cita todos eles.
    /// </summary>
    private bool ConferirCampos()
    {
        var errados = CamposDigitados()
            .Select(digitado => (digitado.Campo, digitado.Nome, digitado.Linha, digitado.Grade, Erro: LeituraCampoMobile.Conferir(digitado.Campo)))
            .Where(digitado => digitado.Erro is not null)
            .ToList();
        if (errados.Count == 0) return true;
        foreach (var errado in errados) errado.Campo.Erro = errado.Erro!.Orientacao;
        var primeiro = errados[0];
        Mensagem = errados.Count > 1
            ? $"Corrija os {errados.Count} campos destacados: {Lista(errados.Select(errado => errado.Nome))}."
            : primeiro.Linha is null ? primeiro.Erro!.Message : $"{primeiro.Linha.Titulo}: {primeiro.Erro!.Message}";
        _avisoDosCampos = true;
        if (primeiro.Grade is not null && primeiro.Linha is not null) primeiro.Grade.Pagina.Exibir(primeiro.Linha);
        Destacar(primeiro.Campo);
        return false;
    }

    // As células citam a linha, como "Valor em Parcela 2", porque o rótulo se repete em todas as linhas.
    private IEnumerable<(CampoMobile Campo, string Nome, LinhaGradeMobile? Linha, GradeMobile? Grade)> CamposDigitados()
    {
        foreach (var campo in _campos.Values.Where(campo => campo.Visivel && campo.EhEntrada))
            yield return (campo, campo.Rotulo, null, null);
        foreach (var grade in Grades.Where(grade => grade.Visivel))
            foreach (var linha in grade.Linhas)
                foreach (var campo in linha.Campos.Where(campo => campo.Visivel && campo.EhEntrada))
                    yield return (campo, $"{campo.Rotulo} em {linha.Titulo}", linha, grade);
    }

    /// <summary>
    /// Falha na leitura ou numa regra do cálculo. O campo que a falha traz, ou que a mensagem cita pelo rótulo, fica destacado;
    /// sem campo, a grade de que a mensagem fala recebe a mensagem abaixo do título. Sem nenhum dos dois, o aviso é geral.
    /// </summary>
    private void InformarErro(string mensagem, CampoMobile? campo = null, string? orientacao = null)
    {
        Mensagem = mensagem;
        if (campo is not null)
        {
            campo.Erro = orientacao ?? OrientacaoDaMensagem(campo, mensagem);
            _avisoDosCampos = true;
            Destacar(campo);
            return;
        }
        var grades = Grades.Where(grade => grade.Visivel).ToList();
        var citados = CamposCitados.Encontrar<object>(mensagem,
            _campos.Values.Where(item => item.Visivel && item.EhEntrada).Select(item => ((object)item, item.Rotulo)),
            grades.Count == 1 ? grades[0] : null);
        foreach (var (alvo, texto) in citados)
        {
            if (alvo is CampoMobile citado) citado.Erro = texto;
            else if (alvo is GradeMobile grade) grade.Erro = texto;
        }
        _avisoDosCampos = citados.Count > 0;
        if (citados.Count == 0) return;
        if (citados[0].Campo is CampoMobile primeiro) Destacar(primeiro);
        else GradeComErro = citados[0].Campo as GradeMobile;
    }

    // "Faltas: informe..." vira "Informe..." abaixo do campo Faltas; sem o rótulo na mensagem, ela vai inteira.
    private static string OrientacaoDaMensagem(CampoMobile campo, string mensagem)
    {
        var inicio = mensagem.IndexOf(campo.Rotulo + ":", StringComparison.Ordinal);
        if (inicio < 0) return mensagem;
        var resto = mensagem[(inicio + campo.Rotulo.Length + 1)..].Trim();
        return resto.Length == 0 ? mensagem : char.ToUpper(resto[0], Cultura) + resto[1..];
    }

    private void Destacar(CampoMobile campo)
    {
        foreach (var secao in Secoes.Where(s => s.Campos.Contains(campo))) secao.Expandida = true;
        PrimeiroCampoComErro = campo;
    }

    private void LimparErros()
    {
        Mensagem = null;
        PrimeiroCampoComErro = null;
        GradeComErro = null;
        _avisoDosCampos = false;
        foreach (var campo in TodosCampos) campo.Erro = null;
        foreach (var grade in Grades) grade.Erro = null;
    }

    // Uma edição tira o destaque do próprio campo; o aviso continua enquanto houver outro campo destacado.
    private void AtualizarAviso()
    {
        if (_avisoDosCampos && (TodosCampos.Any(campo => campo.TemErro) || Grades.Any(grade => grade.TemErro))) return;
        _avisoDosCampos = false;
        Mensagem = null;
    }

    private static string Lista(IEnumerable<string> itens)
    {
        // Muitas células erradas numa grade tornariam o aviso ilegível; os demais campos seguem destacados na tela.
        const int Maximo = 5;
        var lista = itens.ToArray();
        if (lista.Length > Maximo) return $"{string.Join(", ", lista[..Maximo])} e outros {lista.Length - Maximo}";
        return lista.Length == 1 ? lista[0] : $"{string.Join(", ", lista[..^1])} e {lista[^1]}";
    }
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemResultado))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharResumoCommand))]
    private DemonstrativoDto? _demonstrativo;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalcularCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharResumoCommand))]
    private bool _ocupado;

    public abstract string Titulo { get; }
    public long? HistoricoId { get; set; }
    public abstract string Descricao { get; }
    public IReadOnlyList<CampoMobile> Campos => _campos.Values.ToArray();
    public ObservableCollection<LinhaDepositoFgtsMobile> DepositosFgts => _formulario.DepositosFgts;
    public PaginaLinhas<LinhaDepositoFgtsMobile> PaginaDepositosFgts => field ??= new(DepositosFgts);
    public virtual bool ExibirHistoricoFgts => false;
    public virtual bool TemDepositosFgts => false;
    public virtual ICommand? AdicionarDepositoCommand => null;
    public virtual ICommand? RemoverDepositoCommand => null;
    public virtual ICommand? GerarDepositosCommand => null;
    public virtual bool ExibirConsultaCotacoes => false;
    public virtual ICommand? ConsultaCotacoesCommand => null;
    public virtual bool PodeCalcularRra => false;
    public virtual ICommand? CalcularRraCommand => null;
    public virtual bool PodeLevarFgtsRescisao => false;
    public virtual ICommand? LevarFgtsRescisaoCommand => null;
    public virtual bool PodeTransferirJornada => false;
    public virtual ICommand? JornadaParaHoleriteCommand => null;
    public virtual ICommand? JornadaParaHorasExtrasCommand => null;
    public virtual string? AvisoHistorico => null;
    public ObservableCollection<LinhaBeneficioMobile> BeneficiosDetalhados => _formulario.BeneficiosDetalhados;
    public virtual bool ExibirBeneficiosDetalhados => false;
    public virtual bool TemBeneficiosDetalhados => false;
    public virtual ICommand? AdicionarBeneficioCommand => null;
    public virtual ICommand? RemoverBeneficioCommand => null;
    public ObservableCollection<GradeMobile> Grades => _formulario.Grades;
    /// <summary>Desfaz a última remoção nas listas de depósitos do FGTS, benefícios detalhados e lançamentos do banco de horas.</summary>
    public DesfazerRemocaoMobile RemocaoLinhas => _formulario.RemocaoLinhas;

    /// <summary>
    /// A última remoção do formulário, em qualquer grade ou lista, exibida no rodapé junto de Calcular: removida uma linha no
    /// meio de uma lista longa, o topo da grade estaria fora da tela. Uma remoção nova descarta o desfazer da anterior.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemRemocaoAtual))]
    private DesfazerRemocaoMobile? _remocaoAtual;
    public bool TemRemocaoAtual => RemocaoAtual is not null;

    private void AcompanharRemocao(DesfazerRemocaoMobile remocao) => remocao.PropertyChanged += (_, e) =>
    {
        if (e.PropertyName != nameof(DesfazerRemocaoMobile.Visivel)) return;
        if (remocao.Visivel && !ReferenceEquals(RemocaoAtual, remocao))
        {
            var anterior = RemocaoAtual;
            RemocaoAtual = remocao;
            anterior?.Descartar();
        }
        else if (!remocao.Visivel && ReferenceEquals(RemocaoAtual, remocao)) RemocaoAtual = null;
    };
    public ObservableCollection<LinhaLancamentoBancoHorasMobile> LancamentosBancoHoras => _formulario.LancamentosBancoHoras;
    public PaginaLinhas<LinhaLancamentoBancoHorasMobile> PaginaLancamentosBancoHoras => field ??= new(LancamentosBancoHoras);
    public virtual bool ExibirLancamentosBancoHoras => false;
    public virtual bool TemLancamentosBancoHoras => false;
    public virtual ICommand? AdicionarLancamentoCommand => null;
    public virtual ICommand? RemoverLancamentoCommand => null;
    public virtual ICommand? UsarNoHoleriteCommand => null;
    public virtual ICommand? UsarNaRescisaoCommand => null;
    public bool PodeUsarNoHolerite => UsarNoHoleriteCommand is not null && Demonstrativo?.QuitacaoBancoHoras?.Situacao == SituacaoBancoHoras.Fechamento;
    public bool PodeUsarNaRescisao => UsarNaRescisaoCommand is not null && Demonstrativo?.QuitacaoBancoHoras?.Situacao == SituacaoBancoHoras.Rescisao;
    public bool TemQuitacaoBancoHoras => QuitacaoBancoHoras is not null;
    public string QuitacaoBancoHorasTexto => QuitacaoBancoHoras is not { } quitacao ? string.Empty : string.Join("\n",
        [
            $"Fim do ciclo: {quitacao.FimCiclo.ToString("dd/MM/yyyy", Cultura)}",
            $"Salário de referência: {quitacao.SalarioReferencia.ToString("C2", Cultura)} • divisor {quitacao.Divisor.ToString("N2", Cultura)}",
            .. quitacao.Parcelas.Select(parcela => $"{parcela.Minutos / 60}:{parcela.Minutos % 60:00} h a {parcela.Adicional.ToString("N2", Cultura)}%: {parcela.Valor.ToString("C2", Cultura)}"),
            $"Total transferido: {quitacao.Total.ToString("C2", Cultura)} (INSS, IRRF e FGTS: sim)"
        ]);
    public bool TemResultado => Demonstrativo is not null;

    public DadosFormularioMobile ExportarDados() => _formulario.ExportarDados(QuitacaoBancoHoras, QuitacaoConfirmada);

    public Result TentarImportarDados(DadosFormularioMobile dados)
    {
        var resultado = _formulario.TentarImportarDados(dados, this switch
        {
            HoleriteMobileViewModel => SituacaoBancoHoras.Fechamento,
            RescisaoMobileViewModel => SituacaoBancoHoras.Rescisao,
            _ => null
        });
        if (resultado.Falhou) return resultado;
        LimparErros();
        QuitacaoBancoHoras = dados.QuitacaoBancoHoras;
        QuitacaoConfirmada = dados.QuitacaoConfirmada && QuitacaoBancoHoras is not null;
        Demonstrativo = null;
        Mensagem = null;
        NotificarEdicao();
        return Result.Ok();
    }

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);
    partial void OnMensagemChanged(string? value) => OnPropertyChanged(nameof(TemMensagem));
    partial void OnDemonstrativoChanged(DemonstrativoDto? value)
    {
        OnPropertyChanged(nameof(PodeUsarNoHolerite));
        OnPropertyChanged(nameof(PodeUsarNaRescisao));
        OnPropertyChanged(nameof(PodeCalcularRra));
        OnPropertyChanged(nameof(PodeLevarFgtsRescisao));
        OnPropertyChanged(nameof(PodeTransferirJornada));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemQuitacaoBancoHoras))]
    [NotifyPropertyChangedFor(nameof(QuitacaoBancoHorasTexto))]
    private QuitacaoBancoHoras? _quitacaoBancoHoras;
    [ObservableProperty] private bool _quitacaoConfirmada;
    partial void OnQuitacaoBancoHorasChanged(QuitacaoBancoHoras? value)
    {
        Demonstrativo = null;
        NotificarEdicao();
    }
    partial void OnQuitacaoConfirmadaChanged(bool value)
    {
        NotificarEdicao();
        Demonstrativo = null;
        Mensagem = null;
    }

    /// <summary>Recebe a quitação apurada no banco de horas; o usuário confirma as incidências antes de calcular.</summary>
    public void ReceberQuitacao(QuitacaoBancoHoras quitacao)
    {
        QuitacaoBancoHoras = quitacao;
        QuitacaoConfirmada = false;
        PreencherComQuitacao(quitacao);
    }

    protected virtual void PreencherComQuitacao(QuitacaoBancoHoras quitacao) { }

    protected QuitacaoBancoHoras? LerQuitacao() => QuitacaoBancoHoras switch
    {
        null => null,
        { } quitacao when QuitacaoConfirmada => quitacao,
        _ => throw new EntradaInvalidaException("Confira as incidências de INSS, IRRF e FGTS da quitação do banco de horas e marque a confirmação, ou descarte a transferência.")
    };

    [RelayCommand]
    private void DescartarQuitacao()
    {
        QuitacaoBancoHoras = null;
        QuitacaoConfirmada = false;
    }

    protected CampoMobile Adicionar(string chave, string rotulo, TipoCampoMobile tipo, string valor = "", string dica = "", IReadOnlyList<string>? opcoes = null)
    {
        var campo = new CampoMobile(chave, rotulo, dica, tipo, valor, opcoes);
        _campos.Add(chave, campo);
        _secoes = null;
        campo.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(CampoMobile.Valor) or nameof(CampoMobile.Indice) or nameof(CampoMobile.Marcado))
            {
                Demonstrativo = null;
                AtualizarAviso();
                AoAlterar(campo);
                NotificarEdicao();
            }
        };
        return campo;
    }

    /// <summary>Título que agrupa os campos seguintes; não é lido no cálculo.</summary>
    protected CampoMobile Secao(string chave, string titulo) => Adicionar(chave, titulo, TipoCampoMobile.Secao);

    protected virtual void AoAlterar(CampoMobile campo) { }
    protected CampoMobile Campo(string chave) => _campos[chave];
    protected string Texto(string chave) => Campo(chave).Valor.Trim();
    protected bool Sim(string chave) => Campo(chave).Marcado;
    protected int Opcao(string chave) => LeituraCampoMobile.Opcao(Campo(chave));
    protected decimal Numero(string chave) => LeituraCampoMobile.Numero(Campo(chave));
    protected int Inteiro(string chave) => LeituraCampoMobile.Inteiro(Campo(chave));
    protected decimal Horas(string chave) => LeituraCampoMobile.Horas(Campo(chave));
    protected DateOnly Data(string chave) => LeituraCampoMobile.Data(Campo(chave));
    protected DateOnly? DataOpcional(string chave) => LeituraCampoMobile.DataOpcional(Campo(chave));
    protected DateOnly Competencia(string chave) => LeituraCampoMobile.Competencia(Campo(chave));

    protected GradeMobile AdicionarGrade(string chave, string titulo, string descricao, string nomeLinha, int limite,
        IReadOnlyList<ColunaGradeMobile> colunas, Action<LinhaGradeMobile>? aoAlterarLinha = null, bool reordenavel = false)
    {
        var grade = new GradeMobile(chave, titulo, descricao, nomeLinha, limite, colunas, aoAlterarLinha) { Reordenavel = reordenavel };
        grade.Alterada += (_, _) =>
        {
            NotificarEdicao();
            Demonstrativo = null;
            AtualizarAviso();
        };
        AcompanharRemocao(grade.Remocao);
        Grades.Add(grade);
        return grade;
    }

    protected GradeMobile Grade(string chave) => Grades.First(grade => grade.Chave == chave);
    protected void AdicionarPensao()
    {
        Adicionar("pensaoBase", "Pensão alimentícia", TipoCampoMobile.Opcao, dica: "Selecione a base definida na decisão ou no acordo.",
            opcoes: ["Não informar", "Rendimentos líquidos", "Rendimentos brutos", "Salário mínimo", "Valor fixo"]);
        Adicionar("pensaoPercentual", "Pensão (%)", TipoCampoMobile.Numero, "0", "Percentual da base escolhida.");
        Adicionar("pensaoValor", "Pensão fixa (R$)", TipoCampoMobile.Moeda, "0,00", "Valor fixo definido na decisão ou no acordo.");
        AtualizarPensao();
    }

    protected RegraPensao? Pensao() => Opcao("pensaoBase") switch
    {
        1 => new(BasePensao.RendimentosLiquidos, Numero("pensaoPercentual"), 0m),
        2 => new(BasePensao.RendimentosBrutos, Numero("pensaoPercentual"), 0m),
        3 => new(BasePensao.SalarioMinimo, Numero("pensaoPercentual"), 0m),
        4 => new(BasePensao.ValorFixo, 0m, Numero("pensaoValor")),
        _ => null
    };

    protected void AtualizarPensao()
    {
        var baseSelecionada = Campo("pensaoBase").Indice;
        Campo("pensaoPercentual").Visivel = baseSelecionada is >= 1 and <= 3;
        Campo("pensaoValor").Visivel = baseSelecionada == 4;
    }

    protected abstract Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken);
    private bool PodeCalcular() => !Ocupado;
    private bool PodeCompartilhar() => !Ocupado && TemResultado;

    [RelayCommand(CanExecute = nameof(PodeCalcular))]
    private async Task CalcularAsync(CancellationToken cancellationToken)
    {
        Demonstrativo = null;
        LimparErros();
        if (!ConferirCampos()) return;
        try
        {
            Ocupado = true;
            var resultado = await ApurarAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (resultado.Falhou)
            {
                // Uma tabela ausente ou outra falha que não é de preenchimento não aponta campo.
                if (resultado.Erro.Tipo == TipoErro.Validacao) InformarErro(resultado.Erro.Mensagem);
                else Mensagem = resultado.Erro.Mensagem;
            }
            else Demonstrativo = resultado.Valor;
        }
        catch (EntradaInvalidaException erro) { InformarErro(erro.Message, erro.Campo, erro.Orientacao); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha na calculadora móvel {Calculadora}.", Titulo);
            Mensagem = "Não foi possível calcular. Confira os dados e tente novamente.";
        }
        finally { Ocupado = false; }
    }

    [RelayCommand(CanExecute = nameof(PodeCompartilhar))]
    private async Task CompartilharPdfAsync(CancellationToken cancellationToken)
    {
        if (Demonstrativo is not { } demonstrativo) return;
        Ocupado = true;
        Mensagem = null;
        try { await compartilhamento.CompartilharAsync(demonstrativo, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao compartilhar PDF da calculadora {Calculadora}.", Titulo);
            Mensagem = "Não foi possível compartilhar o PDF. Tente novamente.";
        }
        finally { Ocupado = false; }
    }

    [RelayCommand(CanExecute = nameof(PodeCompartilhar))]
    private async Task CompartilharResumoAsync(CancellationToken cancellationToken)
    {
        if (Demonstrativo is not { } demonstrativo) return;
        Mensagem = null;
        try { await compartilhamento.CompartilharResumoAsync(demonstrativo, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception erro)
        {
            logger.LogError(erro, "Falha ao compartilhar o resumo da calculadora {Calculadora}.", Titulo);
            Mensagem = "Não foi possível compartilhar o resumo. Tente novamente.";
        }
    }
}
