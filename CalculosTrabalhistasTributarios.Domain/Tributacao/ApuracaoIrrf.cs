namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

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
    decimal LivroCaixa = 0m)
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
