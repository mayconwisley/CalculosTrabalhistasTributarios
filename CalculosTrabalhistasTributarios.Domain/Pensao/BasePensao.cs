namespace CalculosTrabalhistasTributarios.Domain.Pensao;

/// <summary>Sobre o que a pensão incide, conforme a decisão judicial ou o acordo.</summary>
public enum BasePensao
{
    /// <summary>Rendimentos menos o INSS e o IRRF; como o IRRF também depende da pensão, o cálculo é feito em iterações.</summary>
    RendimentosLiquidos,
    RendimentosBrutos,
    SalarioMinimo,
    ValorFixo
}
