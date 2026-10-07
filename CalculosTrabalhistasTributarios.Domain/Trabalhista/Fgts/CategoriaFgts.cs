namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;

/// <summary>
/// Categoria que define a alíquota do FGTS: 8% em geral, 2% no contrato de aprendizagem (Lei 8.036/1990, art. 15, caput e
/// § 7º) e 8% no doméstico, com mais 3,2% de indenização compensatória (LC 150/2015, art. 34, IV e V).
/// </summary>
public enum CategoriaFgts { Empregado, Aprendiz, Domestico }
