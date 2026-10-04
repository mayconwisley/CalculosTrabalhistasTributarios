namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// Regime da empresa, que define a contribuição patronal sobre a folha. O empregador doméstico paga 8% de contribuição
/// patronal, 0,8% de GILRAT e 3,2% de indenização compensatória, além do FGTS (LC 150/2015, art. 34).
/// </summary>
public enum RegimeTributario { LucroRealOuPresumido, SimplesNacional, SimplesNacionalAnexoIV, EmpregadorDomestico }
