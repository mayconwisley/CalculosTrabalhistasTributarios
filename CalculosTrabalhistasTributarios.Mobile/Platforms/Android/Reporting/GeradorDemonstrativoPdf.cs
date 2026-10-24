using CalculosTrabalhistasTributarios.Application.DTOs;
using System.Globalization;
using static CalculosTrabalhistasTributarios.Mobile.Platforms.Android.Reporting.DiagramacaoPdf;

namespace CalculosTrabalhistasTributarios.Mobile.Platforms.Android.Reporting;

/// <summary>
/// PDF A4 nativo do Android com a organização do demonstrativo do desktop (RelatorioPdfDemonstrativo): resumo, comparativo,
/// verbas com totais e resultado, informativos, memória e observações. Consome a apuração pronta, sem recalcular regras de negócio.
/// </summary>
internal static class GeradorDemonstrativoPdf
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static void Gerar(DemonstrativoDto dto, string caminho, CancellationToken cancellationToken)
    {
        using var pdf = new DiagramacaoPdf(dto.Titulo, $"{dto.Referencia}  |  Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}",
            $"Cálculos Trabalhistas e Tributários - versão {AppInfo.Current.VersionString} - dados processados localmente", cancellationToken);
        if (dto.Destaques.Count > 0)
            Resumo(pdf, dto.Destaques);
        if (dto.Comparativo is { } comparativo)
            Comparativo(pdf, comparativo);
        if (dto.TemVerbas)
            Verbas(pdf, dto);
        else if (dto.Comparativo is null)
            Bloco(pdf, () => pdf.BarraResultado(dto.RotuloResultado, Moeda(dto.Resultado)));
        if (dto.Informativos.Count > 0)
            Informativos(pdf, dto.Informativos);
        foreach (var grupo in dto.Memoria)
            Memoria(pdf, grupo);
        if (dto.Observacoes.Count > 0)
        {
            pdf.Secao("Observações");
            foreach (var observacao in dto.Observacoes)
            {
                pdf.Paragrafo(observacao, prefixo: "• ");
                pdf.Espaco(4);
            }
        }
        pdf.Salvar(caminho);
    }

    private static void Resumo(DiagramacaoPdf pdf, IReadOnlyList<DestaqueDto> destaques) => Bloco(pdf, () =>
    {
        pdf.Secao("Resumo");
        pdf.Tabela([new(string.Empty, 2), new(string.Empty, 2), new(string.Empty, 3)],
            destaques.Select(destaque => new Celula[]
            {
                new(destaque.Rotulo, true, Fundo: AzulClaro),
                new(destaque.Valor, true),
                new(destaque.Complemento, Tamanho: 8)
            }), comCabecalho: false);
    });

    private static void Comparativo(DiagramacaoPdf pdf, TabelaComparativaDto comparativo) => Bloco(pdf, () =>
    {
        pdf.Secao(comparativo.Titulo);
        pdf.Tabela([new(string.Empty, 3), .. comparativo.Colunas.Select(titulo => new Coluna(titulo, 2, Alinhamento.Direita))],
            comparativo.Linhas.Select(linha => (IReadOnlyList<Celula>)[new(linha.Descricao, linha.Destaque), .. linha.Valores.Select(valor => new Celula(valor, linha.Destaque))]));
    });

    private static void Verbas(DiagramacaoPdf pdf, DemonstrativoDto dto) => Bloco(pdf, () =>
    {
        var temDescontos = dto.Descontos.Count > 0;
        List<Coluna> colunas = [new("Descrição", 4), new("Referência", 1.4f), new(dto.RotuloProventos, 1.6f, Alinhamento.Direita)];
        if (temDescontos)
            colunas.Add(new("Descontos", 1.6f, Alinhamento.Direita));
        IReadOnlyList<Celula> Linha(VerbaDto verba, string provento, string desconto) => temDescontos
            ? [new(verba.Descricao), new(verba.Referencia), new(provento), new(desconto)]
            : [new(verba.Descricao), new(verba.Referencia), new(provento)];
        List<IReadOnlyList<Celula>> linhas = [
            .. dto.Proventos.Select(verba => Linha(verba, Moeda(verba.Valor), string.Empty)),
            .. dto.Descontos.Select(verba => Linha(verba, string.Empty, Moeda(verba.Valor)))];
        List<Celula> totais = [new("Totais", true, Fundo: AzulClaro, Abrange: 2), new(Moeda(dto.TotalProventos), true, Fundo: AzulClaro)];
        if (temDescontos)
            totais.Add(new(Moeda(dto.TotalDescontos), true, Fundo: AzulClaro));
        linhas.Add(totais);
        pdf.Secao("Demonstrativo");
        pdf.Tabela(colunas, linhas);
        pdf.BarraResultado(dto.RotuloResultado, Moeda(dto.Resultado));
    });

    private static void Informativos(DiagramacaoPdf pdf, IReadOnlyList<VerbaDto> informativos) => Bloco(pdf, () =>
    {
        pdf.Secao("Valores informativos", "Não entram nos totais nem no resultado");
        pdf.Tabela([new("Descrição", 4), new("Referência", 1.4f), new("Valor", 1.6f, Alinhamento.Direita)],
            informativos.Select(verba => (IReadOnlyList<Celula>)[new(verba.Descricao), new(verba.Referencia), new(Moeda(verba.Valor))]));
    });

    private static void Memoria(DiagramacaoPdf pdf, GrupoMemoriaDto grupo) => Bloco(pdf, () =>
    {
        pdf.Secao($"Memória de cálculo - {grupo.Titulo}", grupo.Destaque, 100);
        pdf.IniciarCaixa();
        foreach (var formula in grupo.Formulas)
            pdf.Formula(formula.Titulo, formula.Formula);
        if (grupo.Quadro is { } quadro)
        {
            pdf.Espaco(6);
            QuadroFaixas(pdf, quadro);
        }
        pdf.FinalizarCaixa();
    });

    /// <summary>As faixas em tabela: a parte da base de cada uma e o valor antes e depois de truncar.</summary>
    private static void QuadroFaixas(DiagramacaoPdf pdf, QuadroFaixasDto quadro)
    {
        Coluna[] colunas =
        [
            new("Faixa", 0.7f, Alinhamento.Direita), new("De", 1.4f, Alinhamento.Direita), new("Até", 1.4f, Alinhamento.Direita),
            new("Parte da base", 1.5f, Alinhamento.Direita), new("Alíquota", 0.9f, Alinhamento.Direita),
            new("Valor exato", 1.6f, Alinhamento.Direita), new("Valor considerado", 1.5f, Alinhamento.Direita)
        ];
        List<IReadOnlyList<Celula>> linhas = [.. quadro.Faixas.Select(faixa => (IReadOnlyList<Celula>)
        [
            new(faixa.Numero.ToString(Cultura), Tamanho: 8), new(Moeda(faixa.De), Tamanho: 8),
            new(faixa.Ate is { } ate ? Moeda(ate) : "Sem limite", Tamanho: 8), new(Moeda(faixa.ParteDaBase), Tamanho: 8),
            new(faixa.Aliquota.ToString("0.##", Cultura) + "%", Tamanho: 8), new(MoedaExata(faixa.ValorExato), Tamanho: 8),
            new(MoedaExata(faixa.Valor), Tamanho: 8)
        ])];
        linhas.Add(
        [
            new("Soma das faixas", true, 8, AzulClaro, Abrange: 3), new(Moeda(quadro.BaseCalculo), true, 8, AzulClaro),
            new(string.Empty, Tamanho: 8, Fundo: AzulClaro), new(MoedaExata(quadro.ValorExato), true, 8, AzulClaro),
            new(Moeda(quadro.Valor), true, 8, AzulClaro)
        ]);
        pdf.Tabela(colunas, linhas, padding: 4, tamanho: 8);
    }

    // Separa os blocos do relatório como o espaçamento de 16 pontos da coluna do desktop.
    private static void Bloco(DiagramacaoPdf pdf, Action conteudo)
    {
        conteudo();
        pdf.Espaco(16);
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);

    /// <summary>Moeda com as casas que o valor tiver, até seis, como na memória: "R$ 115,3656".</summary>
    private static string MoedaExata(decimal valor) => "R$ " + valor.ToString("#,##0.00####", Cultura);
}
