using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class LinhaDepositoFgtsMobile : ObservableObject
{
    private string _competencia = "";
    private string _valor = "0,00";
    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value ?? string.Empty); }
    public string Valor { get => _valor; set => SetProperty(ref _valor, value ?? string.Empty); }
}
