using ClosedXML.Excel;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

/// <summary>Uma aba preenchida de cima para baixo: título, seções, tabelas e textos.</summary>
internal sealed class Aba(IXLWorksheet folha)
{
    private const string AzulPrimario = "1D4ED8";
    private const string AzulClaro = "EFF6FF";
    private int _linha = 1;
    private int _colunas = 2;

    public void Titulo(string titulo, string subtitulo)
    {
        folha.Cell(_linha, 1).SetValue(titulo);
        folha.Cell(_linha, 1).Style.Font.SetBold().Font.SetFontSize(15).Font.SetFontColor(XLColor.FromHtml("#" + AzulPrimario));
        _linha++;
        folha.Cell(_linha++, 1).SetValue(subtitulo);
        folha.Cell(_linha, 1).SetValue($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm} por Cálculos Trabalhistas e Tributários");
        folha.Cell(_linha++, 1).Style.Font.SetFontColor(XLColor.Gray).Font.SetItalic();
    }

    public void Secao(string titulo)
    {
        _linha++;
        var celula = folha.Cell(_linha++, 1);
        celula.SetValue(titulo);
        celula.Style.Font.SetBold().Font.SetFontSize(12);
    }

    public void Cabecalho(params string[] titulos)
    {
        _colunas = Math.Max(_colunas, titulos.Length);
        for (var coluna = 0; coluna < titulos.Length; coluna++)
        {
            var celula = folha.Cell(_linha, coluna + 1);
            celula.SetValue(titulos[coluna]);
            celula.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#" + AzulPrimario));
        }
        _linha++;
    }

    public void Linha(params object?[] valores) => Linha(false, valores);

    public void Linha(bool destaque, params object?[] valores)
    {
        _colunas = Math.Max(_colunas, valores.Length);
        for (var coluna = 0; coluna < valores.Length; coluna++)
        {
            var celula = folha.Cell(_linha, coluna + 1);
            Escrever(celula, valores[coluna]);
            if (destaque)
                celula.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#" + AzulClaro));
        }
        _linha++;
    }

    /// <summary>Rótulo e valor em uma linha, para os resumos.</summary>
    public void Par(string rotulo, object? valor)
    {
        folha.Cell(_linha, 1).SetValue(rotulo);
        Escrever(folha.Cell(_linha, 2), valor);
        _linha++;
    }

    /// <summary>Texto corrido que ocupa a largura da tabela, com quebra de linha.</summary>
    public void Texto(string texto)
    {
        var intervalo = folha.Range(_linha, 1, _linha, Math.Max(_colunas, 4));
        intervalo.Merge();
        intervalo.FirstCell().SetValue(texto);
        intervalo.Style.Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        // A altura acompanha o tamanho do texto, já que o Excel não ajusta a de células mescladas.
        folha.Row(_linha).Height = 15 * Math.Max(1, Math.Ceiling(texto.Length / 110.0));
        _linha++;
    }

    /// <summary>Uma linha da memória de cálculo: o título e a fórmula.</summary>
    public void Formula(string titulo, string formula)
    {
        folha.Cell(_linha, 1).SetValue(titulo);
        folha.Cell(_linha, 1).Style.Font.SetBold();
        var intervalo = folha.Range(_linha, 2, _linha, Math.Max(_colunas, 6));
        intervalo.Merge();
        intervalo.FirstCell().SetValue(formula);
        intervalo.Style.Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        folha.Row(_linha).Height = 15 * Math.Max(1, Math.Ceiling(formula.Length / 95.0));
        _linha++;
    }

    public void Ajustar()
    {
        folha.Columns(1, _colunas).AdjustToContents(4, folha.LastRowUsed()?.RowNumber() ?? 1);
        foreach (var coluna in folha.Columns(1, _colunas))
            coluna.Width = Math.Clamp(coluna.Width, 12, 60);
    }

    private static void Escrever(IXLCell celula, object? valor)
    {
        switch (valor)
        {
            case null:
                break;
            case Celula numero:
                celula.SetValue(numero.Valor);
                celula.Style.NumberFormat.Format = numero.Formato;
                break;
            case DateOnly data:
                celula.SetValue(data.ToDateTime(TimeOnly.MinValue));
                celula.Style.DateFormat.Format = "dd/mm/yyyy";
                break;
            case int inteiro:
                celula.SetValue(inteiro);
                break;
            default:
                celula.SetValue(valor.ToString());
                break;
        }
    }
}
