using System.Globalization;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public abstract partial class CalculadoraTrabalhistaViewModel(ICompartilhamentoPdf compartilhamento, ILogger logger) : ObservableObject
{
    protected static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly Dictionary<string, CampoMobile> _campos = [];
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemResultado))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    private DemonstrativoDto? _demonstrativo;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalcularCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    private bool _ocupado;

    public abstract string Titulo { get; }
    public abstract string Descricao { get; }
    public IReadOnlyList<CampoMobile> Campos => _campos.Values.ToArray();
    public ObservableCollection<LinhaDepositoFgtsMobile> DepositosFgts { get; } = [];
    public PaginaLinhas<LinhaDepositoFgtsMobile> PaginaDepositosFgts => field ??= new(DepositosFgts);
    public virtual bool ExibirHistoricoFgts => false;
    public virtual bool TemDepositosFgts => false;
    public virtual ICommand? AdicionarDepositoCommand => null;
    public virtual ICommand? RemoverDepositoCommand => null;
    public virtual ICommand? GerarDepositosCommand => null;
    public virtual string? AvisoHistorico => null;
    public ObservableCollection<LinhaBeneficioMobile> BeneficiosDetalhados { get; } = [];
    public virtual bool ExibirBeneficiosDetalhados => false;
    public virtual bool TemBeneficiosDetalhados => false;
    public virtual ICommand? AdicionarBeneficioCommand => null;
    public virtual ICommand? RemoverBeneficioCommand => null;
    public ObservableCollection<GradeMobile> Grades { get; } = [];
    public ObservableCollection<LinhaLancamentoBancoHorasMobile> LancamentosBancoHoras { get; } = [];
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
    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);
    partial void OnMensagemChanged(string? value) => OnPropertyChanged(nameof(TemMensagem));
    partial void OnDemonstrativoChanged(DemonstrativoDto? value)
    {
        OnPropertyChanged(nameof(PodeUsarNoHolerite));
        OnPropertyChanged(nameof(PodeUsarNaRescisao));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemQuitacaoBancoHoras))]
    [NotifyPropertyChangedFor(nameof(QuitacaoBancoHorasTexto))]
    private QuitacaoBancoHoras? _quitacaoBancoHoras;
    [ObservableProperty] private bool _quitacaoConfirmada;
    partial void OnQuitacaoBancoHorasChanged(QuitacaoBancoHoras? value) => Demonstrativo = null;
    partial void OnQuitacaoConfirmadaChanged(bool value)
    {
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
        campo.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(CampoMobile.Valor) or nameof(CampoMobile.Indice) or nameof(CampoMobile.Marcado))
            {
                Demonstrativo = null;
                Mensagem = null;
                AoAlterar(campo);
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
        IReadOnlyList<ColunaGradeMobile> colunas, Action<LinhaGradeMobile>? aoAlterarLinha = null)
    {
        var grade = new GradeMobile(chave, titulo, descricao, nomeLinha, limite, colunas, aoAlterarLinha);
        grade.Alterada += (_, _) =>
        {
            Demonstrativo = null;
            Mensagem = null;
        };
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
        Mensagem = null;
        try
        {
            Ocupado = true;
            var resultado = await ApurarAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (resultado.Falhou) Mensagem = resultado.Erro.Mensagem;
            else Demonstrativo = resultado.Valor;
        }
        catch (EntradaInvalidaException erro) { Mensagem = erro.Message; }
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
}
