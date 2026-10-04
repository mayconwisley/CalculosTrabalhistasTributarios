namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <param name="AliquotaEfetiva">Em %: (receita de 12 meses × alíquota nominal − parcela a deduzir) ÷ receita de 12 meses.</param>
public sealed record AliquotaSimples(AnexoSimples Anexo, int Faixa, decimal AliquotaNominal, decimal ParcelaDeduzir, decimal AliquotaEfetiva);
