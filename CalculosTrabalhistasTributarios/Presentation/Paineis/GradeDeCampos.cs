using System.Windows;
using System.Windows.Media;

namespace CalculosTrabalhistasTributarios.Presentation.Paineis;

/// <summary>
/// Distribui os campos do formulário em colunas de mesma largura que ocupam todo o espaço disponível.
/// O número de colunas é o que cabe com a largura mínima de cada uma; os campos marcados com
/// <see cref="LarguraProperty"/> esticam até a borda das colunas que ocupam, e os demais, como as grades
/// de meses, mantêm a própria largura, alinhados à esquerda, como no painel com quebra de linha. Quando um campo
/// largo não cabe no fim da linha e desce, o campo anterior ocupa as colunas que sobraram, em vez de deixar um vão;
/// a ordem dos campos, e com ela a do Tab, não muda.
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
        // Último campo que estica na linha atual, para ocupar as colunas que sobrarem quando o próximo descer.
        int? ultimoQueEstica = null;
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
                // Só um campo que desce abre o vão; antes de um bloco próprio, como a grade de meses, a linha termina ali.
                if (estica && ultimoQueEstica is { } indice)
                    alturaDaLinha = Math.Max(alturaDaLinha, EsticarAteOFim(indice, largura));
                ultimoQueEstica = null;
                topo += alturaDaLinha;
                alturaDaLinha = 0;
                coluna = 0;
            }

            var larguraFinal = estica ? larguraDaColuna * ocupa : Math.Min(filho.DesiredSize.Width, largura);
            filho.Measure(new Size(larguraFinal, double.PositiveInfinity));
            _posicoes.Add(new Posicao(new Rect(coluna * larguraDaColuna, topo, larguraFinal, filho.DesiredSize.Height)));
            alturaDaLinha = Math.Max(alturaDaLinha, filho.DesiredSize.Height);
            coluna += ocupa;
            if (estica)
                ultimoQueEstica = _posicoes.Count - 1;
            else if (coluna < colunas)
                // Um campo de largura própria depois do que estica: esticar aquele o sobreporia a este.
                ultimoQueEstica = null;
            if (coluna >= colunas)
            {
                ultimoQueEstica = null;
                topo += alturaDaLinha;
                alturaDaLinha = 0;
                coluna = 0;
            }
        }
        return new Size(largura, topo + alturaDaLinha);
    }

    // Alarga o campo até a borda direita do painel e devolve a nova altura dele.
    private double EsticarAteOFim(int indice, double largura)
    {
        var area = _posicoes[indice].Area;
        area.Width = largura - area.X;
        var filho = InternalChildren[indice];
        filho.Measure(new Size(area.Width, double.PositiveInfinity));
        area.Height = filho.DesiredSize.Height;
        _posicoes[indice] = new Posicao(area);
        return area.Height;
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
