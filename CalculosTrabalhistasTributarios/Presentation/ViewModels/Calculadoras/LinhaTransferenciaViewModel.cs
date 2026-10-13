namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Uma competência da transferência provisória: o salário da origem e os dias transferido no mês.</summary>
public sealed class LinhaTransferenciaViewModel : LinhaCompetenciaViewModel
{
    private string _salario = "0,00";
    private string _dias = "30";

    public string Salario { get => _salario; set => SetProperty(ref _salario, value); }
    public string Dias { get => _dias; set => SetProperty(ref _dias, value); }
}
