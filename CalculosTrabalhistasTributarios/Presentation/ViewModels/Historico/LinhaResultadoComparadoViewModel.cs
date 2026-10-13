namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

/// <param name="Diferenca">Segundo menos primeiro, em reais; vazia quando os valores não são em reais.</param>
public sealed record LinhaResultadoComparadoViewModel(string Grupo, string Item, string Primeiro, string Segundo, string Diferenca)
{
    public bool Diferente => Primeiro != Segundo;
}
