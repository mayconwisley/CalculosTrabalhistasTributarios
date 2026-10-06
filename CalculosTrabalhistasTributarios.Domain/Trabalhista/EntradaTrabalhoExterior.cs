namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>Um recebimento mensal de fonte estrangeira por pessoa física residente fiscal no Brasil.</summary>
public sealed record EntradaTrabalhoExterior(
    VinculoTrabalhoExterior Vinculo,
    decimal RemuneracaoMoeda,
    decimal JurosAtrasoMoeda,
    decimal ImpostoRetidoMoeda,
    decimal TaxaTransferenciaMoeda,
    decimal JurosCobradosMoeda,
    decimal TaxaTransferenciaReais,
    decimal DolaresPorUnidade,
    decimal DolarCompraFiscal,
    decimal CambioEfetivoReais);
