using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

public sealed class ItemHistoricoViewModel(CalculoSalvoDto calculo) : ViewModelBase
{
    private bool _selecionado;
    public CalculoSalvoDto Calculo { get; } = calculo;
    public string Nome => Calculo.Nome;
    public string Detalhe => $"{Calculo.Calculadora} • alterado em {Calculo.AlteradoEm:dd/MM/yyyy} às {Calculo.AlteradoEm:HH:mm}";
    public bool Selecionado { get => _selecionado; set => SetProperty(ref _selecionado, value); }
}
