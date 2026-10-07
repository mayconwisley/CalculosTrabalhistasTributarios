namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Receita bruta e folha de salários com encargos de uma competência anterior ao período de apuração.</summary>
/// <param name="Folha">Salários e pró-labore pagos, com a CPP e o FGTS recolhidos; sem aluguéis nem lucros distribuídos.</param>
public sealed record MesSimples(DateOnly Competencia, decimal Receita, decimal Folha);
