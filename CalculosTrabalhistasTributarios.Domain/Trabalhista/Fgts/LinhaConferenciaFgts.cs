namespace CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;

/// <param name="Devido">Remuneração × alíquota, arredondado aos centavos.</param>
/// <param name="Compensatoria">No doméstico, os 3,2% da indenização compensatória, depositados à parte; zero nas demais categorias.</param>
/// <param name="Diferenca">Devido menos informado: positiva, falta depositar; negativa, depósito a maior.</param>
/// <param name="Vencimento">Prazo do depósito; nulo no rescisório sem data de desligamento.</param>
public sealed record LinhaConferenciaFgts(LancamentoFgts Lancamento, decimal Devido, decimal Compensatoria, decimal Diferenca, DateOnly? Vencimento);
