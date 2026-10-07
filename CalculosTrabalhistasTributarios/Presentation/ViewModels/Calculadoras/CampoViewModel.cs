using CalculosTrabalhistasTributarios.Presentation.Mvvm;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Campo do formulário de uma calculadora; a janela escolhe o controle pelo tipo do campo.</summary>
public abstract class CampoViewModel(string rotulo, string? dica) : ViewModelBase
{
    private bool _visivel = true;
    private string? _erro;

    public string Rotulo { get; } = rotulo;
    public string? Dica { get; } = dica;

    /// <summary>Campos que só valem para uma opção de outro campo ficam ocultos enquanto ela não está escolhida.</summary>
    public bool Visivel
    {
        get => _visivel;
        set
        {
            // Um campo oculto não entra no cálculo; o seu erro deixaria um aviso que o usuário não consegue corrigir.
            if (SetProperty(ref _visivel, value) && !value)
                MensagemErro = null;
        }
    }

    /// <summary>O que corrigir neste campo, exibido logo abaixo dele; nulo quando o campo não tem erro.</summary>
    public string? MensagemErro
    {
        get => _erro;
        set
        {
            if (SetProperty(ref _erro, value))
                OnPropertyChanged(nameof(TemErro));
        }
    }

    public bool TemErro => _erro is not null;
}
