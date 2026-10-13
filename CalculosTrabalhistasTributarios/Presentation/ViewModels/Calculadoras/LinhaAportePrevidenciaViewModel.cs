using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Um aporte ao plano de previdência: a data, o valor aportado e o saldo atual dele.</summary>
public sealed class LinhaAportePrevidenciaViewModel : ViewModelBase
{
    private string _data = string.Empty;
    private string _aportado = "0,00";
    private string _saldo = "0,00";

    public string Data { get => _data; set => SetProperty(ref _data, value); }
    public string Aportado { get => _aportado; set => SetProperty(ref _aportado, value); }
    public string Saldo { get => _saldo; set => SetProperty(ref _saldo, value); }

    public override string ToString() => Data;
}
