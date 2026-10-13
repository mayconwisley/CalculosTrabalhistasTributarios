using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Uma competência de sobreaviso e prontidão: o salário normal, as horas de cada regime e os feriados do mês.</summary>
public sealed class LinhaSobreavisoViewModel : ViewModelBase
{
    private string _competencia = string.Empty;
    private string _salario = "0,00";
    private string _sobreaviso = "0:00";
    private string _prontidao = "0:00";
    private string _feriados = "0";

    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value); }
    public string Salario { get => _salario; set => SetProperty(ref _salario, value); }
    public string Sobreaviso { get => _sobreaviso; set => SetProperty(ref _sobreaviso, value); }
    public string Prontidao { get => _prontidao; set => SetProperty(ref _prontidao, value); }
    public string Feriados { get => _feriados; set => SetProperty(ref _feriados, value); }

    public override string ToString() => Competencia;
}
