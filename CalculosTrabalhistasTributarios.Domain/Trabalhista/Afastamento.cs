namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>Quem paga cada parte do afastamento e até quando ele vai.</summary>
/// <param name="Dias">Dias de afastamento, contando o primeiro.</param>
/// <param name="DiasEmpresa">Dias pagos pela empresa: os 15 primeiros da doença ou do acidente, a licença-paternidade e a prorrogação da Empresa Cidadã.</param>
/// <param name="DiasPrevidencia">Dias pagos ou reembolsados pela Previdência: a partir do 16º da doença e os 120 da licença-maternidade.</param>
/// <param name="PagoPelaEmpresa">Remuneração dos dias a cargo da empresa.</param>
/// <param name="BeneficioMensal">Valor mensal do benefício da Previdência: 91% da média na doença, a remuneração na maternidade.</param>
/// <param name="PagoPelaPrevidencia">Benefício dos dias a cargo da Previdência.</param>
/// <param name="DepositoFgts">FGTS de todo o período: na doença comum, só dos dias pagos pela empresa.</param>
/// <param name="FimEstabilidade">Fim da garantia de emprego, quando houver.</param>
public sealed record Afastamento(
    TipoAfastamento Tipo,
    DateOnly Inicio,
    DateOnly Fim,
    int Dias,
    int DiasEmpresa,
    int DiasPrevidencia,
    decimal PagoPelaEmpresa,
    decimal BeneficioMensal,
    decimal PagoPelaPrevidencia,
    decimal DepositoFgts,
    DateOnly? FimEstabilidade)
{
    public DateOnly Retorno => Fim.AddDays(1);

    /// <summary>Doença ou acidente de mais de 6 meses no período aquisitivo tira o direito às férias dele (CLT, art. 133, IV).</summary>
    public bool PodePerderFerias => Tipo is TipoAfastamento.Doenca or TipoAfastamento.AcidenteDeTrabalho && Dias > 180;
}
