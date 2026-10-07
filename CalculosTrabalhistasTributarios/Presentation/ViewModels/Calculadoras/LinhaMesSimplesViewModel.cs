using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Receita bruta e folha com encargos de uma competência anterior ao período de apuração do Simples Nacional.</summary>
public sealed class LinhaMesSimplesViewModel : ViewModelBase
{
    private string _competencia = string.Empty;
    private string _receita = "0,00";
    private string _folha = "0,00";

    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value); }
    public string Receita { get => _receita; set => SetProperty(ref _receita, value); }
    public string Folha { get => _folha; set => SetProperty(ref _folha, value); }

    public override string ToString() => Competencia;
}
