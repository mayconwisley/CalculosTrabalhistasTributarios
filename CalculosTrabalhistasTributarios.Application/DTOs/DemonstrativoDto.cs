using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>
/// Resultado das calculadoras no formato de holerite: proventos, descontos e o resultado (o líquido, ou o custo total),
/// além de valores informativos que não entram nos totais nem no resultado, como o FGTS, e da memória de cálculo passo a passo.
/// </summary>
/// <param name="Referencia">Período ou data do cálculo, exibido abaixo do título.</param>
/// <param name="Observacoes">Premissas e limites do cálculo que o usuário precisa conhecer.</param>
/// <param name="ParcelasRra">Diferenças por competência que podem seguir para o cálculo do IR sobre rendimentos acumulados.</param>
/// <param name="Comparativo">Comparação lado a lado, exibida antes do demonstrativo; sem proventos e descontos, ela o substitui.</param>
public sealed record DemonstrativoDto(
    string Titulo,
    string Referencia,
    IReadOnlyList<DestaqueDto> Destaques,
    IReadOnlyList<VerbaDto> Proventos,
    IReadOnlyList<VerbaDto> Descontos,
    IReadOnlyList<VerbaDto> Informativos,
    IReadOnlyList<GrupoMemoriaDto> Memoria,
    IReadOnlyList<string> Observacoes,
    string RotuloProventos = "Proventos",
    string RotuloResultado = "Líquido a receber",
    TabelaComparativaDto? Comparativo = null,
    QuitacaoBancoHoras? QuitacaoBancoHoras = null,
    IReadOnlyList<ParcelaRra>? ParcelasRra = null)
{
    /// <summary>Falso quando o cálculo não tem proventos nem descontos, como uma comparação de cenários.</summary>
    public bool TemVerbas => Proventos.Count > 0 || Descontos.Count > 0;

    public decimal TotalProventos => Proventos.Sum(verba => verba.Valor);
    public decimal TotalDescontos => Descontos.Sum(verba => verba.Valor);
    public decimal Resultado => TotalProventos - TotalDescontos;
}
