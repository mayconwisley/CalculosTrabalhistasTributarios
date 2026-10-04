namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Parâmetros tributários e previdenciários vigentes em uma competência, como estão cadastrados.</summary>
/// <param name="SalarioMinimo">Nulo quando não há salário mínimo cadastrado até a competência.</param>
/// <param name="FaixasPlr">Tabela anual da PLR vigente; vazia quando não há tabela cadastrada.</param>
/// <param name="FaixasSalarioFamilia">Faixas do salário-família vigentes; vazias quando não há tabela cadastrada.</param>
/// <param name="FaixasSeguroDesemprego">Faixas do seguro-desemprego: limite da média, percentual sobre o excedente (Aliquota) e valor fixo (Deducao).</param>
public sealed record PerfilTributario(
    IReadOnlyList<FaixaTributaria> FaixasInss,
    IReadOnlyList<FaixaTributaria> FaixasIrrf,
    decimal DeducaoSimplificada,
    decimal DeducaoPorDependente,
    decimal DescontoMinimo,
    IReadOnlyList<RegraReducaoMensalIrrf> ReducoesMensaisIrrf,
    decimal? SalarioMinimo = null,
    IReadOnlyList<FaixaTributaria>? FaixasPlr = null,
    IReadOnlyList<FaixaSalarioFamilia>? FaixasSalarioFamilia = null,
    IReadOnlyList<FaixaTributaria>? FaixasSeguroDesemprego = null);
