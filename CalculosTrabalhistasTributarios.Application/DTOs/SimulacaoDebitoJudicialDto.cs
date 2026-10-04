using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record SimulacaoDebitoJudicialDto(
    NaturezaDebito Natureza,
    DateOnly DataCalculo,
    IReadOnlyList<ParcelaDebitoDto> Parcelas,
    IReadOnlyList<string> Criterios,
    IReadOnlyList<string> Observacoes,
    decimal Multa = 0m,
    decimal Honorarios = 0m)
{
    public decimal TotalValor => Parcelas.Sum(parcela => parcela.Valor);
    public decimal TotalAtualizado => Parcelas.Sum(parcela => parcela.Atualizado);
    public decimal TotalCorrecao => TotalAtualizado - TotalValor;
    public decimal TotalJuros => Parcelas.Sum(parcela => parcela.Juros);
    public decimal Total => Parcelas.Sum(parcela => parcela.Total);
    public bool TemAcrescimos => Multa + Honorarios > 0m;
    public decimal TotalComAcrescimos => Total + Multa + Honorarios;
    public string NomeNatureza => Natureza == NaturezaDebito.Trabalhista ? "Débito trabalhista" : "Débito cível";
}
