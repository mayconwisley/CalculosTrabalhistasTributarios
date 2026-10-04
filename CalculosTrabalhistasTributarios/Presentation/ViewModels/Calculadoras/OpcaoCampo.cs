namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed record OpcaoCampo(string Texto, object Valor)
{
    public override string ToString() => Texto;
}
