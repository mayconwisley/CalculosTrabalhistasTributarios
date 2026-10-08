namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

public sealed record LinhaComparacaoHistoricoViewModel(string Campo, string Primeiro, string Segundo)
{
    public bool Diferente => Primeiro != Segundo;
}
