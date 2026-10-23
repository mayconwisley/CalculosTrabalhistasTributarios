using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;

/// <summary>Tabelas do menu, nos grupos e na ordem do desktop.</summary>
public static class CatalogoTabelasMobile
{
    public static IReadOnlyList<GrupoTabelasMobile> Grupos { get; } =
    [
        new("INSS e salário",
        [
            new(TipoTabelaTributaria.Inss, "Tabela INSS", "Faixas e alíquotas da contribuição do empregado."),
            new(TipoTabelaTributaria.SalarioMinimo, "Salário mínimo", "Valor nacional, base do adicional de insalubridade."),
            new(TipoTabelaTributaria.SalarioFamilia, "Salário-família", "Limite de remuneração e cota por filho."),
            new(TipoTabelaTributaria.SeguroDesemprego, "Seguro-desemprego", "Faixas da média salarial e valor máximo da parcela.")
        ]),
        new("Imposto de renda",
        [
            new(TipoTabelaTributaria.Irrf, "Tabela IRRF", "Faixas mensais, alíquotas e parcelas a deduzir."),
            new(TipoTabelaTributaria.ReducaoMensalIrrf, "Redução mensal do IRRF", "Redução do imposto conforme os rendimentos do mês."),
            new(TipoTabelaTributaria.Plr, "Tabela PLR", "Tabela anual exclusiva da participação nos lucros."),
            new(TipoTabelaTributaria.DescontoMinimo, "Desconto mínimo", "IRRF até este valor não é retido (Lei 9.430/1996).")
        ]),
        new("Deduções do IRRF",
        [
            new(TipoTabelaTributaria.Simplificado, "Valor simplificado", "Desconto que substitui as deduções legais."),
            new(TipoTabelaTributaria.Dependente, "Dedução por dependente", "Valor deduzido da base do IRRF por dependente.")
        ]),
        new("Índices econômicos",
        [
            new(TipoTabelaTributaria.Inpc, "INPC", "Inflação mensal do IBGE, usada na correção monetária."),
            new(TipoTabelaTributaria.Ipca, "IPCA", "Inflação mensal oficial do IBGE, usada na correção monetária."),
            new(TipoTabelaTributaria.TaxaLegal, "Taxa legal", "Juros de mora mensais desde 30/08/2024, publicados pelo Banco Central."),
            new(TipoTabelaTributaria.Selic, "Selic", "Taxa mensal do Banco Central, dos débitos judiciais até 29/08/2024."),
            new(TipoTabelaTributaria.IpcaE, "IPCA-E", "IPCA-15 mensal do IBGE, da fase pré-judicial trabalhista."),
            new(TipoTabelaTributaria.Tr, "TR", "Taxa referencial mensal, juros da fase pré-judicial trabalhista.")
        ])
    ];

    public static IEnumerable<CartaoTabelaMobile> Cartoes => Grupos.SelectMany(grupo => grupo.Cartoes);

    public static CartaoTabelaMobile Cartao(TipoTabelaTributaria tipo) => Cartoes.First(cartao => cartao.Tipo == tipo);
}
