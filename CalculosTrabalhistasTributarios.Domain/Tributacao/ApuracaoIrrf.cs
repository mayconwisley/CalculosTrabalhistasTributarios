namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <param name="Tabela">Faixas da tabela progressiva usada, da menor para a maior, para a memória mostrar os limites.</param>
/// <param name="RegrasReducao">Regras da redução mensal da competência; vazias quando não há redução.</param>
public sealed record ApuracaoIrrf(
    decimal Rendimentos,
    decimal Inss,
    int Dependentes,
    decimal DeducaoPorDependente,
    decimal? DescontoSimplificado,
    ModalidadeIrrf Normal,
    ModalidadeIrrf Simplificada,
    decimal Pensao = 0m,
    decimal LimiteDispensa = 0m,
    decimal PrevidenciaComplementar = 0m,
    decimal LivroCaixa = 0m,
    IReadOnlyList<FaixaTributaria>? Tabela = null,
    IReadOnlyList<RegraReducaoMensalIrrf>? RegrasReducao = null)
{
    // A fonte pagadora aplica o desconto simplificado quando ele resulta em imposto menor que o das deduções legais.
    public bool SimplificadaAplicada => DescontoSimplificado is not null && Simplificada.Imposto < Normal.Imposto;
    public ModalidadeIrrf Aplicada => SimplificadaAplicada ? Simplificada : Normal;

    /// <summary>Imposto da modalidade aplicada, antes da dispensa de retenção.</summary>
    public decimal ImpostoCalculado => Aplicada.Imposto;

    /// <summary>O IRRF de até R$ 10,00 não é retido nem se acumula para o mês seguinte (Lei 9.430/1996, art. 67).</summary>
    public bool RetencaoDispensada => ImpostoCalculado > 0m && ImpostoCalculado <= LimiteDispensa;

    /// <summary>Imposto retido: o calculado, ou zero quando a retenção é dispensada.</summary>
    public decimal Imposto => RetencaoDispensada ? 0m : ImpostoCalculado;
}
