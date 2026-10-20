using System.Windows;
using System.Windows.Media;

namespace CalculosTrabalhistasTributarios.Presentation.Paineis;

/// <summary>
/// Distribui os campos do formulário em colunas de mesma largura que ocupam todo o espaço disponível.
/// O número de colunas é o que cabe com a largura mínima de cada uma; os campos marcados com
/// <see cref="LarguraProperty"/> esticam até a borda das colunas que ocupam, e os demais, como as grades
/// de meses, mantêm a própria largura, alinhados à esquerda, como no painel com quebra de linha.
/// </summary>
public sealed class GradeDeCampos : System.Windows.Controls.Panel
{
    /// <summary>Largura mínima de uma coluna, sem o espaçamento à direita de cada campo.</summary>
    public static readonly DependencyProperty LarguraColunaProperty = DependencyProperty.Register(
        nameof(LarguraColuna), typeof(double), typeof(GradeDeCampos),
        new FrameworkPropertyMetadata(170d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Espaço entre as colunas, já incluído na margem direita que cada campo declara.</summary>
    public static readonly DependencyProperty EspacamentoProperty = DependencyProperty.Register(
        nameof(Espacamento), typeof(double), typeof(GradeDeCampos),
        new FrameworkPropertyMetadata(14d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// Largura de referência do campo, na raiz do seu modelo: define quantas colunas ele ocupa
    /// (um campo de 290 ocupa duas de 170) e o faz esticar até a borda delas.
    /// </summary>
    public static readonly DependencyProperty LarguraProperty = DependencyProperty.RegisterAttached(
        "Largura", typeof(double), typeof(GradeDeCampos),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    // Sem largura definida, como dentro de uma rolagem horizontal, o painel usa este número de colunas.
    private const int ColunasSemLargura = 5;

    private readonly List<Posicao> _posicoes = [];

    public double LarguraColuna
    {
        get => (double)GetValue(LarguraColunaProperty);
        set => SetValue(LarguraColunaProperty, value);
    }

    public double Espacamento
    {
        get => (double)GetValue(EspacamentoProperty);
        set => SetValue(EspacamentoProperty, value);
    }

    public static double GetLargura(DependencyObject elemento) => (double)elemento.GetValue(LarguraProperty);
    public static void SetLargura(DependencyObject elemento, double valor) => elemento.SetValue(LarguraProperty, valor);

    protected override Size MeasureOverride(Size disponivel)
    {
        var passo = LarguraColuna + Espacamento;
        var largura = double.IsInfinity(disponivel.Width) ? passo * ColunasSemLargura : disponivel.Width;
        var colunas = Math.Max(1, (int)Math.Floor((largura + 0.5) / passo));
        var larguraDaColuna = largura / colunas;

        _posicoes.Clear();
        double topo = 0, alturaDaLinha = 0;
        var coluna = 0;
        foreach (UIElement filho in InternalChildren)
        {
            filho.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (filho.DesiredSize.Width <= 0 && filho.DesiredSize.Height <= 0)
            {
                _posicoes.Add(Posicao.Oculta);
                continue;
            }

            var referencia = LarguraDeReferencia(filho);
            var estica = !double.IsNaN(referencia);
            var larguraNatural = estica ? referencia + Espacamento : filho.DesiredSize.Width;
            var ocupa = Math.Clamp((int)Math.Ceiling(larguraNatural / passo - 0.001), 1, colunas);
            if (coluna > 0 && coluna + ocupa > colunas)
            {
                topo += alturaDaLinha;
                alturaDaLinha = 0;
                coluna = 0;
            }

            var larguraFinal = estica ? larguraDaColuna * ocupa : Math.Min(filho.DesiredSize.Width, largura);
            filho.Measure(new Size(larguraFinal, double.PositiveInfinity));
            _posicoes.Add(new Posicao(new Rect(coluna * larguraDaColuna, topo, larguraFinal, filho.DesiredSize.Height)));
            alturaDaLinha = Math.Max(alturaDaLinha, filho.DesiredSize.Height);
            coluna += ocupa;
            if (coluna >= colunas)
            {
                topo += alturaDaLinha;
                alturaDaLinha = 0;
                coluna = 0;
            }
        }
        return new Size(largura, topo + alturaDaLinha);
    }

    protected override Size ArrangeOverride(Size final)
    {
        for (var indice = 0; indice < InternalChildren.Count && indice < _posicoes.Count; indice++)
            InternalChildren[indice].Arrange(_posicoes[indice].Area);
        return final;
    }

    // O item do ItemsControl é um ContentPresenter; a largura de referência fica na raiz do modelo do campo.
    private static double LarguraDeReferencia(UIElement filho)
    {
        var propria = GetLargura(filho);
        if (!double.IsNaN(propria) || VisualTreeHelper.GetChildrenCount(filho) == 0)
            return propria;
        return VisualTreeHelper.GetChild(filho, 0) is DependencyObject raiz ? GetLargura(raiz) : double.NaN;
    }

    private readonly record struct Posicao(Rect Area)
    {
        public static Posicao Oculta { get; } = new(new Rect());
    }
}
