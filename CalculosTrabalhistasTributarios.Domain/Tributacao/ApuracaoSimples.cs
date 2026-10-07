namespace CalculosTrabalhistasTributarios.Domain.Tributacao;

/// <summary>DAS do período de apuração e os elementos que definiram o anexo e a alíquota.</summary>
/// <param name="FatorR">Razão exata entre a folha e a receita, ou o valor fixado pela resolução; nulo fora do fator r.</param>
/// <param name="CasoFatorR">Explicação do valor do fator r quando a folha ou a receita é zero ou no início de atividade.</param>
/// <param name="AnexoIII">Alíquota pelo Anexo III, só nas atividades sujeitas ao fator r, para comparação.</param>
/// <param name="AnexoV">Alíquota pelo Anexo V, só nas atividades sujeitas ao fator r, para comparação.</param>
/// <param name="CppForaDoDas">No Anexo IV, estimativa de 20% sobre salários e pró-labore, recolhida em guia própria.</param>
public sealed record ApuracaoSimples(
    JanelaSimples Janela,
    IReadOnlyList<MesSimples> MesesConsiderados,
    decimal ReceitaMes,
    decimal Rbt12,
    decimal Fs12,
    decimal? FatorR,
    string? CasoFatorR,
    AliquotaSimples Aliquota,
    decimal Das,
    AliquotaSimples? AnexoIII,
    AliquotaSimples? AnexoV,
    decimal CppForaDoDas)
{
    public bool AcimaDoSublimite => Rbt12 > SimplesNacional.SublimiteIcmsIss;
    public decimal DasPelo(AliquotaSimples aliquota) => CalculadoraTributacao.Arredondar(ReceitaMes * aliquota.AliquotaEfetiva / 100m);
}
