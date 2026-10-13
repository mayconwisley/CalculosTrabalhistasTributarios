using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Uma linha de grade mensal, identificada pela competência (MM/AAAA).</summary>
public abstract class LinhaCompetenciaViewModel : ViewModelBase
{
    private string _competencia = string.Empty;

    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value); }

    public override string ToString() => Competencia;
}
