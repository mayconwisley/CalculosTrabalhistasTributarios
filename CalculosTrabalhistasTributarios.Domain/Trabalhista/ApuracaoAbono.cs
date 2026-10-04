namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <param name="MotivosSemDireito">Requisitos não cumpridos; vazio quando há direito.</param>
public sealed record ApuracaoAbono(int Meses, decimal SalarioMinimo, decimal Limite, IReadOnlyList<string> MotivosSemDireito, decimal Valor)
{
    public bool TemDireito => MotivosSemDireito.Count == 0;
}
