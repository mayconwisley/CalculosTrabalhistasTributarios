namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <param name="BaseInformada">Base antes da limitação ao teto.</param>
public sealed record ApuracaoInss(decimal BaseInformada, decimal BaseConsiderada, decimal Valor, IReadOnlyList<ResultadoFaixaTributaria> Detalhes)
{
    public bool LimitadaAoTeto => BaseConsiderada < BaseInformada;
}
