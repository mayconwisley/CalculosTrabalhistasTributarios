using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="CorrecaoAte">Último mês com índice aplicado; nulo sem correção.</param>
/// <param name="Criterios">Como a correção, os juros e os ritos foram calculados, em frases para o usuário.</param>
/// <param name="Multa">Multa de 10% sobre o débito do rito da penhora (CPC, art. 523, § 1º), quando incluída.</param>
/// <param name="Honorarios">Honorários de 10% sobre o débito do rito da penhora, sem a multa, quando incluídos.</param>
public sealed record SimulacaoPensaoAtrasoDto(
    IReadOnlyList<ParcelaAtrasoDto> Parcelas,
    DateOnly DataCalculo,
    DateOnly? DataAjuizamento,
    CorrecaoMonetaria Correcao,
    JurosDeMora Juros,
    DateOnly? CorrecaoAte,
    IReadOnlyList<string> Criterios,
    IReadOnlyList<string> Observacoes,
    decimal Multa = 0m,
    decimal Honorarios = 0m)
{
    public decimal TotalDevido => Parcelas.Sum(parcela => parcela.Devido);
    public decimal TotalPago => Parcelas.Sum(parcela => parcela.Pago);
    public decimal TotalSaldo => Parcelas.Sum(parcela => parcela.Saldo);
    public decimal TotalCorrecao => Parcelas.Sum(parcela => parcela.Corrigido - parcela.Saldo);
    public decimal TotalJuros => Parcelas.Sum(parcela => parcela.Juros);
    public decimal TotalAtualizado => Parcelas.Sum(parcela => parcela.Total);
    public decimal TotalPrisao => Parcelas.Where(parcela => parcela.RitoPrisao).Sum(parcela => parcela.Total);
    public decimal TotalPenhora => Parcelas.Where(parcela => !parcela.RitoPrisao).Sum(parcela => parcela.Total);
    public bool TemAcrescimos => Multa + Honorarios > 0m;
    public decimal TotalComAcrescimos => TotalAtualizado + Multa + Honorarios;
    public decimal TotalPenhoraComAcrescimos => TotalPenhora + Multa + Honorarios;
    public int QuantidadePrisao => Parcelas.Count(parcela => parcela.RitoPrisao);
    public int QuantidadePenhora => Parcelas.Count(parcela => !parcela.RitoPrisao);

    public static string NomeCorrecao(CorrecaoMonetaria correcao) => correcao switch
    {
        CorrecaoMonetaria.Inpc => "INPC",
        CorrecaoMonetaria.Ipca => "IPCA",
        _ => "sem correção"
    };
}
