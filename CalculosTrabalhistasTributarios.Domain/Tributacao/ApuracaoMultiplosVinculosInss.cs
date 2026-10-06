namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

public sealed record ApuracaoMultiplosVinculosInss(
    IReadOnlyList<ContribuicaoVinculoInss> Vinculos,
    decimal Teto)
{
    public decimal RemuneracaoTotal => Vinculos.Sum(item => item.Vinculo.Remuneracao);
    public decimal BaseTotalTributada => Vinculos.Sum(item => item.BaseTributada);
    public decimal ContribuicaoTotal => Vinculos.Sum(item => item.Contribuicao);
    public decimal RemuneracaoAcimaDoTeto => RemuneracaoTotal - BaseTotalTributada;
}
