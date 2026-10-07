namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>
/// IRRF dos rendimentos recebidos acumuladamente pela tabela progressiva acumulada: limites e parcelas a deduzir da
/// tabela mensal multiplicados pela quantidade de meses (Lei 7.713/1988, art. 12-A, § 1º; IN RFB 1.500/2014, art. 37 e Anexo IV).
/// </summary>
/// <param name="Meses">Quantidade de meses (NM) usada na tabela, com até uma casa decimal.</param>
/// <param name="ParcelaADeduzirMensal">Parcela a deduzir mensal exata da faixa, sem o arredondamento da tabela publicada.</param>
/// <param name="Faixas">Base e imposto de cada faixa da tabela acumulada.</param>
/// <param name="Reducao">Redução da Lei 15.270/2025 com os limites multiplicados pelos meses; zero quando não se aplica.</param>
public sealed record ApuracaoIrrfAcumulado(
    decimal Meses,
    decimal RendimentosTributaveis,
    decimal BaseCalculo,
    decimal Aliquota,
    decimal ParcelaADeduzirMensal,
    IReadOnlyList<ResultadoFaixaTributaria> Faixas,
    decimal ImpostoAntesReducao,
    bool ReducaoAplicavel,
    decimal Reducao,
    decimal Imposto);
