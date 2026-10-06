using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class MediaVerbasMesViewModel : ViewModelBase
{
    private string _competencia = string.Empty;
    private string _comissoes = "0,00";
    private string _dsr = "0,00";
    private string _horasExtras = "0,00";
    private string _adicionais = "0,00";
    private string _outras = "0,00";

    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value); }
    public string Comissoes { get => _comissoes; set => SetProperty(ref _comissoes, value); }
    public string Dsr { get => _dsr; set => SetProperty(ref _dsr, value); }
    public string HorasExtras { get => _horasExtras; set => SetProperty(ref _horasExtras, value); }
    public string Adicionais { get => _adicionais; set => SetProperty(ref _adicionais, value); }
    public string Outras { get => _outras; set => SetProperty(ref _outras, value); }
}
