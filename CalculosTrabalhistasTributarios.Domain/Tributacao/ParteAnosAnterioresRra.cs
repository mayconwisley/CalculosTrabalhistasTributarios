namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>Parte de anos anteriores, tributada exclusivamente na fonte pela tabela acumulada (Lei 7.713/1988, art. 12-A).</summary>
/// <param name="MesesTotais">Meses a que se referem as parcelas: competências mensais distintas e um mês por 13º de cada ano.</param>
/// <param name="Anos">Anos-calendário das parcelas, em ordem.</param>
public sealed record ParteAnosAnterioresRra(
    decimal Parcelas,
    decimal Correcao,
    decimal Despesas,
    decimal Inss,
    decimal Pensao,
    int MesesTotais,
    IReadOnlyList<int> Anos,
    ApuracaoIrrfAcumulado Irrf)
{
    public decimal Rendimentos => Parcelas + Correcao;
}
