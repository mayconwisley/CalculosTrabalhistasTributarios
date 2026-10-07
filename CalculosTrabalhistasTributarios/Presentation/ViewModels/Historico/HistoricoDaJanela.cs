using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

/// <summary>
/// O botão "Salvar no histórico" de uma janela de cálculo e a situação exibida ao lado dele. O cálculo aberto do
/// histórico é substituído ao salvar, a menos que o usuário peça para salvar como novo.
/// </summary>
public sealed class HistoricoDaJanela : ViewModelBase
{
    private readonly IHistoricoCalculos _historico;
    private readonly INomeDialogService _dialogo;
    private readonly IUserNotifier _notificador;
    private readonly ICalculoSalvavel _calculo;
    private long? _id;
    private string? _nome;
    private string _situacao = string.Empty;

    public HistoricoDaJanela(IHistoricoCalculos historico, INomeDialogService dialogo, IUserNotifier notificador, ICalculoSalvavel calculo)
    {
        _historico = historico;
        _dialogo = dialogo;
        _notificador = notificador;
        _calculo = calculo;
        SalvarCommand = new AsyncRelayCommand(SalvarAsync);
    }

    public ICommand SalvarCommand { get; }

    /// <summary>O formulário passou a corresponder ao histórico: foi aberto dele ou acabou de ser salvo.</summary>
    public event Action? Sincronizado;

    public string Situacao
    {
        get => _situacao;
        private set
        {
            if (SetProperty(ref _situacao, value))
                OnPropertyChanged(nameof(TemSituacao));
        }
    }

    public bool TemSituacao => Situacao.Length > 0;

    public async Task CarregarAsync(CalculoSalvoDto salvo)
    {
        _calculo.ImportarDados(DadosFormulario.DeJson(salvo.Dados));
        _id = salvo.Id;
        _nome = salvo.Nome;
        Sincronizado?.Invoke();
        Situacao = $"Aberto do histórico: “{salvo.Nome}”, salvo em {salvo.AlteradoEm:dd/MM/yyyy} às {salvo.AlteradoEm:HH:mm}.";
        await _calculo.RecalcularAsync();
    }

    private async Task SalvarAsync()
    {
        var escolha = _dialogo.SolicitarNome("Salvar no histórico", "Salvar", _nome ?? _calculo.NomeSugerido, oferecerComoNovo: _id is not null);
        if (escolha is null)
            return;

        try
        {
            _id = await _historico.SalvarAsync(escolha.ComoNovo ? null : _id, _calculo.TipoHistorico, _calculo.NomeCalculadora, escolha.Nome, _calculo.ExportarDados().ParaJson(), CancellationToken.None);
            _nome = escolha.Nome;
            Sincronizado?.Invoke();
            Situacao = $"Salvo no histórico como “{escolha.Nome}” às {DateTime.Now:HH:mm}.";
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível salvar o cálculo no histórico.", exception);
        }
    }
}
