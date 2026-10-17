namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>IRRF apurado em uma modalidade, deduções legais ou desconto simplificado, com a redução mensal.</summary>
/// <param name="AliquotaEfetiva">Imposto sobre os rendimentos tributáveis, em %, truncado em duas casas.</param>
/// <param name="DetalhesProgressivos">
/// Base e imposto exato de cada faixa da tabela progressiva, sem arredondar, para conferir o imposto calculado pela parcela a deduzir.
/// </param>
public sealed record ModalidadeIrrf(
    string Nome,
    decimal BaseCalculo,
    decimal Aliquota,
    decimal Deducao,
    decimal ImpostoAntesReducao,
    decimal ReducaoMensal,
    decimal Imposto,
    decimal AliquotaEfetiva,
    IReadOnlyList<ResultadoFaixaTributaria> DetalhesProgressivos)
{
    /// <summary>Modalidade que não existia na competência, como o desconto simplificado antes de 05/2023.</summary>
    public static ModalidadeIrrf Indisponivel(string nome) => new(nome, 0m, 0m, 0m, 0m, 0m, 0m, 0m, []);
}
