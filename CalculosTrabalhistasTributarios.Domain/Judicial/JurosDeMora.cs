namespace CalculosTrabalhistasTributarios.Domain.Judicial;

/// <summary>
/// Juros de mora sobre as parcelas corrigidas. Desde 30/08/2024, vale a taxa legal publicada pelo Banco Central: a Selic
/// menos o IPCA-15, nunca negativa (Código Civil, art. 406, com a redação da Lei 14.905/2024, e Resolução CMN 5.171/2024);
/// antes, 1% ao mês.
/// </summary>
public enum JurosDeMora { UmPorCentoAteTaxaLegal, UmPorCento, TaxaLegal, Nenhum }
