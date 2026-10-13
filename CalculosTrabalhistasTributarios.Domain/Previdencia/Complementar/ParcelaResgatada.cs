namespace CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;

/// <summary>A parte de um aporte consumida pelo resgate, com a base e o IR do regime regressivo.</summary>
/// <param name="Base">PGBL: o valor resgatado; VGBL: a parte dele que corresponde aos rendimentos do aporte.</param>
/// <param name="Aliquota">Alíquota regressiva pelo prazo entre o aporte e o resgate.</param>
public sealed record ParcelaResgatada(AportePrevidencia Aporte, decimal Resgatado, decimal Base, decimal Aliquota, decimal Imposto);
