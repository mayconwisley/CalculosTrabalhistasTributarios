using System.Globalization;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
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
    public virtual bool ExibirHistoricoFgts => false;
    public virtual bool TemDepositosFgts => false;
    public virtual ICommand? AdicionarDepositoCommand => null;
    public virtual ICommand? RemoverDepositoCommand => null;
    public virtual ICommand? GerarDepositosCommand => null;
    public virtual string? AvisoHistorico => null;
    public bool TemResultado => Demonstrativo is not null;
    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);
    partial void OnMensagemChanged(string? value) => OnPropertyChanged(nameof(TemMensagem));

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

    protected virtual void AoAlterar(CampoMobile campo) { }
    protected CampoMobile Campo(string chave) => _campos[chave];
    protected string Texto(string chave) => Campo(chave).Valor.Trim();
    protected bool Sim(string chave) => Campo(chave).Marcado;
    protected int Opcao(string chave)
    {
        var campo = Campo(chave);
        if (campo.Indice < 0 || campo.Indice >= campo.Opcoes.Count)
            throw new EntradaInvalidaException($"{campo.Rotulo}: selecione uma opção válida.");
        return campo.Indice;
    }
    protected decimal Numero(string chave)
    {
        var campo = Campo(chave);
        if (!LeituraNumerica.TentarLer(campo.Valor, out var valor))
            throw new EntradaInvalidaException($"{campo.Rotulo}: informe um número, por exemplo 3.500,00.");
        return valor;
    }
    protected int Inteiro(string chave)
    {
        var campo = Campo(chave);
        if (!int.TryParse(campo.Valor.Trim(), NumberStyles.Integer, Cultura, out var valor))
            throw new EntradaInvalidaException($"{campo.Rotulo}: informe um número inteiro.");
        return valor;
    }
    protected decimal Horas(string chave)
    {
        var texto = Texto(chave);
        var partes = texto.Split(':');
        if (partes.Length == 2 && int.TryParse(partes[0], out var horas) && int.TryParse(partes[1], out var minutos)
            && horas >= 0 && minutos is >= 0 and < 60)
            return horas + minutos / 60m;
        if (LeituraNumerica.TentarLer(texto, out var decimais)) return decimais;
        throw new EntradaInvalidaException($"{Campo(chave).Rotulo}: use horas de relógio como 10:30 ou horas decimais como 10,5.");
    }
    protected DateOnly Data(string chave)
    {
        if (DateOnly.TryParseExact(Texto(chave), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data)) return data;
        throw new EntradaInvalidaException($"{Campo(chave).Rotulo}: informe uma data em dd/MM/yyyy.");
    }
    protected DateOnly? DataOpcional(string chave) => Texto(chave).Length == 0 ? null : Data(chave);
    protected DateOnly Competencia(string chave)
    {
        if (DateOnly.TryParseExact("01/" + Texto(chave), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data)) return data;
        throw new EntradaInvalidaException($"{Campo(chave).Rotulo}: informe mês e ano em MM/yyyy.");
    }

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
