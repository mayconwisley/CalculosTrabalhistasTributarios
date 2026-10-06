namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public sealed record ApuracaoTrabalhoExterior(
    decimal CotacaoFiscalReais,
    decimal RemuneracaoFiscal,
    decimal JurosRecebidosFiscal,
    decimal RendimentosTributaveis,
    decimal ImpostoExteriorFiscal,
    decimal RemuneracaoEfetiva,
    decimal JurosRecebidosEfetivos,
    decimal ImpostoExteriorEfetivo,
    decimal TaxaMoedaEfetiva,
    decimal JurosBancoEfetivos,
    decimal TaxaReais,
    decimal CreditoBanco);
