namespace CalculosTrabalhistasTributarios.Domain.Financeiro;

public sealed record EntradaEmprestimoPessoal(
    decimal ValorSolicitado, int NumeroParcelas, decimal JurosMensais,
    DateOnly DataLiberacao, bool IofAutomatico = true, decimal IofInformado = 0m,
    bool FinanciarIof = true, decimal SeguroFinanciado = 0m,
    decimal OutrosCustosFinanciados = 0m, decimal CustosNaLiberacao = 0m);
