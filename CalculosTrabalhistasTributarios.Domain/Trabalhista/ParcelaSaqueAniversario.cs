namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <param name="Faixa">Faixa da tabela em que o saldo caiu, de 1 a 7.</param>
/// <param name="AcimaDe">Limite da faixa anterior; zero na primeira.</param>
public sealed record ParcelaSaqueAniversario(int Faixa, decimal AcimaDe, decimal Aliquota, decimal ParcelaAdicional, decimal Valor);
