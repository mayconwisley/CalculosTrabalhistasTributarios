using Android.Graphics;
using Android.Graphics.Pdf;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using Cor = Android.Graphics.Color;
using Paint = Android.Graphics.Paint;

namespace CalculosTrabalhistasTributarios.Mobile.Platforms.Android.Reporting;

/// <summary>
/// Página A4 no estilo dos relatórios do desktop (ComponentesPdf, com QuestPDF), desenhada pelo PdfDocument nativo do Android:
/// cabeçalho, seções, tabelas, barra do resultado e o rodapé com o aviso de simulação em todas as páginas. O conteúdo é medido
/// e distribuído nas páginas antes de ser desenhado, para que o rodapé saiba o total de páginas.
/// </summary>
internal sealed class DiagramacaoPdf : IDisposable
{
    // As cores de ComponentesPdf, para que o PDF do celular e o do computador tenham a mesma aparência.
    public static readonly Cor AzulPrimario = Cor.Rgb(0x1D, 0x4E, 0xD8);
    public static readonly Cor AzulClaro = Cor.Rgb(0xEF, 0xF6, 0xFF);
    public static readonly Cor CinzaBorda = Cor.Rgb(0xD1, 0xD5, 0xDB);
    public static readonly Cor CinzaTexto = Cor.Rgb(0x4B, 0x55, 0x63);
    public static readonly Cor Branco = Cor.White;

    private const int Largura = 595;
    private const int Altura = 842;
    private const float Margem = 36;
    private const float LarguraUtil = Largura - 2 * Margem;
    private const float PaddingCaixa = 8;

    private static readonly Estilo TituloPagina = new(20, true, AzulPrimario);
    private static readonly Estilo Subtitulo = new(9, false, CinzaTexto);
    private static readonly Estilo TituloSecao = new(12, true, AzulPrimario);
    private static readonly Estilo Etiqueta = new(8, true, AzulPrimario);
    private static readonly Estilo Corpo = new(9, false, CinzaTexto);
    private static readonly Estilo AvisoTitulo = new(9, true, AzulPrimario);
    private static readonly Estilo Informacao = new(8, false, CinzaTexto);

    private readonly CancellationToken _cancellationToken;
    private readonly Paint _pincel = new(PaintFlags.AntiAlias);
    private readonly List<List<Action<Canvas>>> _paginas = [];
    private readonly IReadOnlyList<string> _linhasTitulo;
    private readonly IReadOnlyList<string> _linhasSubtitulo;
    private readonly IReadOnlyList<string> _linhasAvisoTitulo;
    private readonly IReadOnlyList<string> _linhasAviso;
    private readonly string _identificacao;
    private readonly float _inicioConteudo;
    private readonly float _topoRodape;
    private readonly float _limiteConteudo;
    private float _y;
    private float _x = Margem;
    private float _largura = LarguraUtil;
    private bool _paginaVazia;
    private (int Pagina, float Topo)? _caixa;

    public DiagramacaoPdf(string titulo, string subtitulo, string identificacao, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _identificacao = identificacao;
        _linhasTitulo = Quebrar(titulo, TituloPagina, LarguraUtil);
        _linhasSubtitulo = Quebrar(subtitulo, Subtitulo, LarguraUtil);
        _linhasAvisoTitulo = Quebrar(AvisoSimulacao.Titulo, AvisoTitulo, LarguraUtil - 2 * PaddingCaixa);
        _linhasAviso = Quebrar(AvisoSimulacao.Orientacao, Corpo, LarguraUtil - 2 * PaddingCaixa);
        _inicioConteudo = Margem + AlturaCabecalho + 16;
        _topoRodape = Altura - Margem - AlturaRodape;
        _limiteConteudo = _topoRodape - 12;
    }

    public enum Alinhamento { Esquerda, Direita }

    public sealed record Coluna(string Titulo, float Peso, Alinhamento Alinhamento = Alinhamento.Esquerda);

    /// <summary>Célula de tabela; com fundo, não leva a linha inferior, como as células destacadas do desktop.</summary>
    public sealed record Celula(string Texto, bool Negrito = false, float Tamanho = 9, Cor? Fundo = null, Cor? CorTexto = null, int Abrange = 1);

    private sealed record Estilo(float Tamanho, bool Negrito, Cor Cor, bool Monoespacado = false);

    private float AlturaCabecalho => _linhasTitulo.Count * Entrelinha(20) + 4 + _linhasSubtitulo.Count * Entrelinha(9) + 8 + 1;

    private float AlturaAviso => 2 * PaddingCaixa + _linhasAvisoTitulo.Count * Entrelinha(9) + 3 + _linhasAviso.Count * Entrelinha(9);

    private float AlturaRodape => AlturaAviso + 8 + Entrelinha(8);

    // Dentro da moldura, o espaçamento inferior dela também precisa caber antes do rodapé.
    private float LimiteAtual => _limiteConteudo - (_caixa is null ? 0 : PaddingCaixa);

    public void Espaco(float altura) => _y += altura;

    public void Secao(string titulo, string? destaque = null, float manterComProximo = 60)
    {
        var etiqueta = string.IsNullOrWhiteSpace(destaque) ? null : Quebrar(destaque, Etiqueta, _largura * 0.45f);
        var larguraEtiqueta = etiqueta is null ? 0 : etiqueta.Max(linha => Medir(linha, Etiqueta)) + 16;
        var linhas = Quebrar(titulo, TituloSecao, _largura - (etiqueta is null ? 0 : larguraEtiqueta + 8));
        var alturaEtiqueta = etiqueta is null ? 0 : etiqueta.Count * Entrelinha(8) + 8;
        var alturaTitulo = Math.Max(linhas.Count * Entrelinha(12), alturaEtiqueta);
        var x = _x;
        var largura = _largura;
        // O título não fica sozinho no fim da página, separado do conteúdo.
        Reservar(alturaTitulo + 17 + manterComProximo);
        Desenhar(alturaTitulo + 17, (canvas, topo) =>
        {
            DesenharLinhas(canvas, linhas, TituloSecao, x, topo, largura, Alinhamento.Esquerda);
            if (etiqueta is not null)
            {
                var esquerda = x + largura - larguraEtiqueta;
                Retangulo(canvas, esquerda, topo, larguraEtiqueta, alturaEtiqueta, AzulClaro);
                DesenharLinhas(canvas, etiqueta, Etiqueta, esquerda + 8, topo + 4, larguraEtiqueta - 16, Alinhamento.Esquerda);
            }
            Linha(canvas, x, topo + alturaTitulo + 8, x + largura, CinzaBorda);
        });
    }

    /// <summary>Texto corrido, quebrado em linhas que podem passar para a página seguinte. O prefixo, como um marcador, recua as demais linhas.</summary>
    public void Paragrafo(string texto, float tamanho = 9, bool negrito = false, Cor? cor = null, bool monoespacado = false, string prefixo = "")
    {
        var estilo = new Estilo(tamanho, negrito, cor ?? CinzaTexto, monoespacado);
        var recuo = prefixo.Length == 0 ? 0 : Medir(prefixo, estilo);
        var linhas = Quebrar(texto, estilo, _largura - recuo);
        // Parágrafos curtos passam inteiros para a página seguinte, sem deixar uma linha isolada; os longos se dividem.
        if (linhas.Count <= 6)
            Reservar(linhas.Count * Entrelinha(tamanho));
        for (var indice = 0; indice < linhas.Count; indice++)
        {
            var linha = indice == 0 ? prefixo + linhas[indice] : linhas[indice];
            var x = indice == 0 ? _x : _x + recuo;
            var largura = _largura;
            Desenhar(Entrelinha(tamanho), (canvas, topo) => DesenharLinhas(canvas, [linha], estilo, x, topo, largura, Alinhamento.Esquerda));
        }
    }

    /// <summary>Fórmula da memória, como AdicionarFormulaIrrf do desktop; o título não fica sozinho no fim da página.</summary>
    public void Formula(string titulo, string formula)
    {
        Reservar(Entrelinha(8) * Quebrar(titulo, Etiqueta, _largura).Count + Entrelinha(8));
        Paragrafo(titulo, 8, true, AzulPrimario);
        Paragrafo(formula, 8, monoespacado: true);
        Espaco(3);
    }

    public void Tabela(IReadOnlyList<Coluna> colunas, IEnumerable<IReadOnlyList<Celula>> linhas, bool comCabecalho = true, float padding = 6, float tamanho = 9)
    {
        var cabecalho = colunas.Select(coluna => new Celula(coluna.Titulo, true, tamanho, AzulPrimario, Branco)).ToArray();
        var corpo = linhas.ToList();
        var primeira = corpo.Count > 0 ? AlturaLinha(colunas, corpo[0], padding) : 0;
        Reservar((comCabecalho ? AlturaLinha(colunas, cabecalho, padding) : 0) + primeira);
        if (comCabecalho)
            LinhaTabela(colunas, cabecalho, padding);
        foreach (var linha in corpo)
        {
            if (!_paginaVazia && _y + AlturaLinha(colunas, linha, padding) > LimiteAtual)
            {
                NovaPagina();
                // O cabeçalho se repete na página seguinte, para que as colunas continuem identificadas.
                if (comCabecalho)
                    LinhaTabela(colunas, cabecalho, padding);
            }
            LinhaTabela(colunas, linha, padding);
        }
    }

    public void BarraResultado(string rotulo, string valor)
    {
        var estiloRotulo = new Estilo(13, true, Branco);
        var estiloValor = new Estilo(15, true, Branco);
        var larguraValor = Medir(valor, estiloValor);
        var linhas = Quebrar(rotulo, estiloRotulo, _largura - 24 - larguraValor - 12);
        var altura = 24 + Math.Max(linhas.Count * Entrelinha(13), Entrelinha(15));
        var x = _x;
        var largura = _largura;
        Desenhar(altura, (canvas, topo) =>
        {
            Retangulo(canvas, x, topo, largura, altura, AzulPrimario);
            DesenharLinhas(canvas, linhas, estiloRotulo, x + 12, topo + 12, largura - 24 - larguraValor - 12, Alinhamento.Esquerda);
            DesenharLinhas(canvas, [valor], estiloValor, x + 12, topo + 12, largura - 24, Alinhamento.Direita);
        });
    }

    /// <summary>Abre a moldura da memória de cálculo; se o conteúdo passar de página, ela é fechada e reaberta.</summary>
    public void IniciarCaixa()
    {
        Reservar(48);
        _caixa = (_paginas.Count - 1, _y);
        _y += PaddingCaixa;
        _x += PaddingCaixa;
        _largura -= 2 * PaddingCaixa;
    }

    public void FinalizarCaixa()
    {
        _y += PaddingCaixa;
        FecharSegmentoDaCaixa(_y);
        _caixa = null;
        _x -= PaddingCaixa;
        _largura += 2 * PaddingCaixa;
    }

    public void Salvar(string caminho)
    {
        if (_paginas.Count == 0)
            NovaPagina();
        using var documento = new PdfDocument();
        try
        {
            for (var indice = 0; indice < _paginas.Count; indice++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                using var info = new PdfDocument.PageInfo.Builder(Largura, Altura, indice + 1).Create();
                var pagina = documento.StartPage(info) ?? throw new InvalidOperationException("Não foi possível iniciar a página do PDF.");
                // PdfDocument não pode ser fechado com uma página em aberto, inclusive após cancelamento.
                try
                {
                    var canvas = pagina.Canvas ?? throw new InvalidOperationException("Página PDF sem canvas.");
                    DesenharCabecalho(canvas);
                    foreach (var desenho in _paginas[indice])
                        desenho(canvas);
                    DesenharRodape(canvas, indice + 1, _paginas.Count);
                }
                finally
                {
                    documento.FinishPage(pagina);
                    pagina.Dispose();
                }
            }
            _cancellationToken.ThrowIfCancellationRequested();
            using var arquivo = File.Create(caminho);
            documento.WriteTo(arquivo);
        }
        finally
        {
            documento.Close();
        }
    }

    public void Dispose() => _pincel.Dispose();

    private void LinhaTabela(IReadOnlyList<Coluna> colunas, IReadOnlyList<Celula> celulas, float padding)
    {
        var posicoes = PosicoesDasCelulas(colunas, celulas);
        var textos = celulas.Select((celula, indice) => Quebrar(celula.Texto, EstiloDa(celula), posicoes[indice].Largura - 2 * padding)).ToArray();
        var altura = AlturaLinha(colunas, celulas, padding);
        Desenhar(altura, (canvas, topo) =>
        {
            for (var indice = 0; indice < celulas.Count; indice++)
            {
                var (x, largura, alinhamento) = posicoes[indice];
                if (celulas[indice].Fundo is { } fundo)
                    Retangulo(canvas, x, topo, largura, altura, fundo);
                else
                    Linha(canvas, x, topo + altura - 0.5f, x + largura, CinzaBorda);
                DesenharLinhas(canvas, textos[indice], EstiloDa(celulas[indice]), x + padding, topo + padding, largura - 2 * padding, alinhamento);
            }
        });
    }

    private float AlturaLinha(IReadOnlyList<Coluna> colunas, IReadOnlyList<Celula> celulas, float padding)
    {
        var posicoes = PosicoesDasCelulas(colunas, celulas);
        return celulas.Select((celula, indice) => Quebrar(celula.Texto, EstiloDa(celula), posicoes[indice].Largura - 2 * padding).Count * Entrelinha(celula.Tamanho))
            .DefaultIfEmpty(0).Max() + 2 * padding;
    }

    private (float X, float Largura, Alinhamento Alinhamento)[] PosicoesDasCelulas(IReadOnlyList<Coluna> colunas, IReadOnlyList<Celula> celulas)
    {
        var unidade = _largura / colunas.Sum(coluna => coluna.Peso);
        var posicoes = new (float, float, Alinhamento)[celulas.Count];
        var x = _x;
        var coluna = 0;
        for (var indice = 0; indice < celulas.Count && coluna < colunas.Count; indice++)
        {
            var abrange = Math.Clamp(celulas[indice].Abrange, 1, colunas.Count - coluna);
            var largura = colunas.Skip(coluna).Take(abrange).Sum(item => item.Peso) * unidade;
            posicoes[indice] = (x, largura, colunas[coluna + abrange - 1].Alinhamento);
            x += largura;
            coluna += abrange;
        }
        return posicoes;
    }

    private static Estilo EstiloDa(Celula celula) => new(celula.Tamanho, celula.Negrito, celula.CorTexto ?? CinzaTexto);

    private void Desenhar(float altura, Action<Canvas, float> desenho)
    {
        Reservar(altura);
        var topo = _y;
        _paginas[^1].Add(canvas => desenho(canvas, topo));
        _y += altura;
        _paginaVazia = false;
    }

    private void Reservar(float altura)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_paginas.Count == 0 || (!_paginaVazia && _y + altura > LimiteAtual))
            NovaPagina();
    }

    private void NovaPagina()
    {
        if (_caixa is not null)
            FecharSegmentoDaCaixa(_y + PaddingCaixa);
        _paginas.Add([]);
        _y = _inicioConteudo;
        _paginaVazia = true;
        if (_caixa is not null)
        {
            _caixa = (_paginas.Count - 1, _y);
            _y += PaddingCaixa;
        }
    }

    private void FecharSegmentoDaCaixa(float fundo)
    {
        if (_caixa is not { } caixa)
            return;
        var x = _x - PaddingCaixa;
        var largura = _largura + 2 * PaddingCaixa;
        _paginas[caixa.Pagina].Add(canvas => Contorno(canvas, x, caixa.Topo, largura, fundo - caixa.Topo, CinzaBorda));
    }

    private void DesenharCabecalho(Canvas canvas)
    {
        var y = Margem;
        DesenharLinhas(canvas, _linhasTitulo, TituloPagina, Margem, y, LarguraUtil, Alinhamento.Esquerda);
        y += _linhasTitulo.Count * Entrelinha(20) + 4;
        DesenharLinhas(canvas, _linhasSubtitulo, Subtitulo, Margem, y, LarguraUtil, Alinhamento.Esquerda);
        y += _linhasSubtitulo.Count * Entrelinha(9) + 8;
        Linha(canvas, Margem, y + 0.5f, Margem + LarguraUtil, AzulPrimario);
    }

    private void DesenharRodape(Canvas canvas, int pagina, int total)
    {
        var y = _topoRodape;
        Retangulo(canvas, Margem, y, LarguraUtil, AlturaAviso, AzulClaro);
        Contorno(canvas, Margem, y, LarguraUtil, AlturaAviso, CinzaBorda);
        y += PaddingCaixa;
        DesenharLinhas(canvas, _linhasAvisoTitulo, AvisoTitulo, Margem + PaddingCaixa, y, LarguraUtil - 2 * PaddingCaixa, Alinhamento.Esquerda);
        y += _linhasAvisoTitulo.Count * Entrelinha(9) + 3;
        DesenharLinhas(canvas, _linhasAviso, Corpo, Margem + PaddingCaixa, y, LarguraUtil - 2 * PaddingCaixa, Alinhamento.Esquerda);
        y = _topoRodape + AlturaAviso + 8;
        var numeracao = $"Página {pagina} de {total}";
        var larguraNumeracao = Medir(numeracao, Informacao);
        var identificacao = Quebrar(_identificacao, Informacao, LarguraUtil - larguraNumeracao - 12)[0];
        DesenharLinhas(canvas, [identificacao], Informacao, Margem, y, LarguraUtil, Alinhamento.Esquerda);
        DesenharLinhas(canvas, [numeracao], Informacao, Margem, y, LarguraUtil, Alinhamento.Direita);
    }

    private void DesenharLinhas(Canvas canvas, IReadOnlyList<string> linhas, Estilo estilo, float x, float topo, float largura, Alinhamento alinhamento)
    {
        var pincel = Pincel(estilo);
        for (var indice = 0; indice < linhas.Count; indice++)
        {
            var linha = linhas[indice];
            var esquerda = alinhamento == Alinhamento.Direita ? x + largura - pincel.MeasureText(linha) : x;
            canvas.DrawText(linha, esquerda, topo + indice * Entrelinha(estilo.Tamanho) + estilo.Tamanho, pincel);
        }
    }

    private IReadOnlyList<string> Quebrar(string? texto, Estilo estilo, float largura)
    {
        var pincel = Pincel(estilo);
        var linhas = new List<string>();
        foreach (var paragrafo in (texto ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
        {
            // Só o fim é aparado: os espaços iniciais alinham as colunas das fórmulas da memória.
            var restante = paragrafo.TrimEnd();
            if (restante.Length == 0)
                linhas.Add(string.Empty);
            while (restante.Length > 0)
            {
                var quantidade = Math.Max(1, pincel.BreakText(restante, true, Math.Max(largura, 1), null));
                if (quantidade < restante.Length)
                {
                    var espaco = restante.LastIndexOf(' ', quantidade - 1, quantidade);
                    if (espaco > 0)
                        quantidade = espaco;
                }
                linhas.Add(restante[..quantidade]);
                restante = restante[quantidade..].TrimStart();
            }
        }
        return linhas;
    }

    private float Medir(string texto, Estilo estilo) => Pincel(estilo).MeasureText(texto);

    private static float Entrelinha(float tamanho) => tamanho * 1.3f;

    private Paint Pincel(Estilo estilo)
    {
        _pincel.SetStyle(Paint.Style.Fill);
        _pincel.TextSize = estilo.Tamanho;
        _pincel.Color = estilo.Cor;
        _pincel.SetTypeface(estilo.Monoespacado ? Typeface.Monospace : estilo.Negrito ? Typeface.DefaultBold : Typeface.Default);
        return _pincel;
    }

    private void Retangulo(Canvas canvas, float x, float y, float largura, float altura, Cor cor)
    {
        _pincel.SetStyle(Paint.Style.Fill);
        _pincel.Color = cor;
        canvas.DrawRect(x, y, x + largura, y + altura, _pincel);
    }

    private void Contorno(Canvas canvas, float x, float y, float largura, float altura, Cor cor)
    {
        _pincel.SetStyle(Paint.Style.Stroke);
        _pincel.StrokeWidth = 1;
        _pincel.Color = cor;
        canvas.DrawRect(x + 0.5f, y + 0.5f, x + largura - 0.5f, y + altura - 0.5f, _pincel);
        _pincel.SetStyle(Paint.Style.Fill);
    }

    private void Linha(Canvas canvas, float inicio, float y, float fim, Cor cor)
    {
        _pincel.StrokeWidth = 1;
        _pincel.Color = cor;
        canvas.DrawLine(inicio, y, fim, y, _pincel);
    }
}
