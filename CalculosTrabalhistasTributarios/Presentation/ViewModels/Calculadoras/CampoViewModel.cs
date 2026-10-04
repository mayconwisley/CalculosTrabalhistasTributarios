using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Campo do formulário de uma calculadora; a janela escolhe o controle pelo tipo do campo.</summary>
public abstract class CampoViewModel(string rotulo, string? dica) : ViewModelBase
{
    private bool _visivel = true;

    public string Rotulo { get; } = rotulo;
    public string? Dica { get; } = dica;

    /// <summary>Campos que só valem para uma opção de outro campo ficam ocultos enquanto ela não está escolhida.</summary>
    public bool Visivel { get => _visivel; set => SetProperty(ref _visivel, value); }
}
