namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

public sealed record ContribuicaoVinculoInss(
    VinculoInss Vinculo,
    decimal BaseAnteriorNoTeto,
    decimal BaseProgressivaAnterior,
    decimal BaseTributada,
    decimal Contribuicao,
    IReadOnlyList<ResultadoFaixaTributaria> Faixas)
{
    public decimal ParcelaAcimaDoTeto => Vinculo.Remuneracao - BaseTributada;
}
