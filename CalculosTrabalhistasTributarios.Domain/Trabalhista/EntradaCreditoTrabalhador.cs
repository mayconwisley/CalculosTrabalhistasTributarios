namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public sealed record EntradaCreditoTrabalhador(ObjetivoCreditoTrabalhador Objetivo, decimal ValorDesejado,
    int NumeroParcelas, decimal JurosMensais, decimal IofFinanciado, decimal OutrosCustosFinanciados,
    decimal CustosDescontadosNaLiberacao, decimal RemuneracaoDisponivel, decimal ParcelasExistentes,
    decimal? MargemLivreOficial = null, bool IofAutomatico = false,
    DateOnly? DataLiberacao = null, DateOnly? PrimeiroVencimento = null);
