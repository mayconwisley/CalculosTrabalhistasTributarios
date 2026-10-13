namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Uma competência de gorjetas: a parte do empregado no rateio, antes da retenção da empresa.</summary>
public sealed class LinhaGorjetaViewModel : LinhaCompetenciaViewModel
{
    private string _valor = "0,00";

    public string Valor { get => _valor; set => SetProperty(ref _valor, value); }
}
