using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Competências seguidas com o mesmo salário de contribuição: de, até (MM/AAAA) e o salário.</summary>
public sealed class LinhaSalarioContribuicaoViewModel : ViewModelBase
{
    private string _inicio = string.Empty;
    private string _fim = string.Empty;
    private string _salario = "0,00";

    public string Inicio { get => _inicio; set => SetProperty(ref _inicio, value); }
    public string Fim { get => _fim; set => SetProperty(ref _fim, value); }
    public string Salario { get => _salario; set => SetProperty(ref _salario, value); }

    public override string ToString() => string.IsNullOrWhiteSpace(Fim) || Fim.Trim() == Inicio.Trim() ? Inicio : $"{Inicio} a {Fim}";
}
