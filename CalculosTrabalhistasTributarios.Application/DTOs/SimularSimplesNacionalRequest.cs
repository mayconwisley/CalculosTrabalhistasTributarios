using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="ProLaboreAtual">Pró-labore mensal do sócio, para estimar o INSS e o IRRF de um aumento que leve o fator r a 28%.</param>
/// <param name="Dependentes">Dependentes do sócio no IRRF do pró-labore.</param>
public sealed record SimularSimplesNacionalRequest(EntradaSimples Entrada, decimal ProLaboreAtual, int Dependentes);
