using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class LinhaDepositoFgtsViewModel : ViewModelBase
{
    private string _competencia = string.Empty;
    private string _valor = "0,00";

    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value); }
    public string Valor { get => _valor; set => SetProperty(ref _valor, value); }
}
