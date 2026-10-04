namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>
/// O vínculo muda o FGTS: o doméstico tem também a indenização compensatória de 3,2% no lugar da multa de 40%
/// (LC 150/2015, art. 22), e o jovem aprendiz tem depósito de 2% (Lei 8.036/1990, art. 15, § 7º).
/// </summary>
public enum TipoVinculo { Empregado, Domestico, Aprendiz }
