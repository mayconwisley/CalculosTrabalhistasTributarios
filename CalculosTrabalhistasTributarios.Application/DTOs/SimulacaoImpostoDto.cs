namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="DeducaoPorDependente">Valor da tabela deduzido da base normal para cada dependente.</param>
/// <param name="DescontoSimplificado">Valor da tabela deduzido da base simplificada; nulo antes de 05/2023, quando a modalidade não existia.</param>
/// <param name="DescontoMinimo">Limite da dispensa de retenção: o IRRF calculado até este valor não é descontado.</param>
public sealed record SimulacaoImpostoDto(
    SimularImpostoRequest Entrada,
    decimal BaseInssConsiderada,
    decimal ValorInss,
    ModalidadeIrrfDto Normal,
    ModalidadeIrrfDto Simplificada,
    decimal DescontoMinimo,
    decimal FgtsOitoPorCento,
    decimal FgtsDoisPorCento,
    string MensagemVantagem,
    string? ModalidadeMaisVantajosa,
    IReadOnlyList<DetalheFaixaDto> DetalhesInss,
    decimal DeducaoPorDependente,
    decimal? DescontoSimplificado)
{
    // A fonte pagadora aplica o desconto simplificado quando ele resulta em imposto menor que o das deduções legais.
    public bool SimplificadaAplicada => DescontoSimplificado is not null && Simplificada.Imposto < Normal.Imposto;

    /// <summary>Imposto da modalidade aplicada, antes da dispensa de retenção.</summary>
    public decimal IrrfCalculado => SimplificadaAplicada ? Simplificada.Imposto : Normal.Imposto;

    /// <summary>O IRRF de até <see cref="DescontoMinimo"/> (R$ 10,00) não é retido (Lei 9.430/1996, art. 67).</summary>
    public bool RetencaoDispensada => IrrfCalculado > 0m && IrrfCalculado <= DescontoMinimo;

    public decimal IrrfAplicado => RetencaoDispensada ? 0m : IrrfCalculado;
    public decimal SalarioLiquido => Entrada.ValorBruto - ValorInss - IrrfAplicado;
}
