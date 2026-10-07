using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.Behaviors;

/// <summary>
/// Máscara dos campos de data (<c>dd/MM/aaaa</c>) e de competência (<c>MM/aaaa</c>): o campo aceita só algarismos e
/// insere as barras enquanto o usuário digita. Digitar a barra depois de um único algarismo completa o zero à esquerda
/// ("1/" vira "01/"), e colar uma data em outro formato comum, como "1/5/2026" ou "2026-05-01", a converte. O texto
/// continua sendo o valor do campo; a leitura e a validação da data permanecem nos ViewModels.
/// </summary>
public static class MascaraData
{
    public const string Data = "Data";
    public const string Competencia = "Competencia";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] FormatosData = ["d/M/yyyy", "d-M-yyyy", "d.M.yyyy", "yyyy-M-d", "ddMMyyyy"];
    private static readonly string[] FormatosCompetencia = ["M/yyyy", "M-yyyy", "M.yyyy", "yyyy-M", "MMyyyy"];
    private static bool _formatando;

    public static readonly DependencyProperty TipoProperty = DependencyProperty.RegisterAttached(
        "Tipo",
        typeof(string),
        typeof(MascaraData),
        new PropertyMetadata(null, AoAlterarTipo));

    public static string? GetTipo(DependencyObject objeto) => (string?)objeto.GetValue(TipoProperty);

    public static void SetTipo(DependencyObject objeto, string? valor) => objeto.SetValue(TipoProperty, valor);

    /// <summary>Quantidade de algarismos do tipo: 8 na data e 6 na competência; 0 quando o tipo não usa máscara.</summary>
    public static int Algarismos(string? tipo) => tipo switch
    {
        Data => 8,
        Competencia => 6,
        _ => 0
    };

    /// <summary>Monta o texto mascarado a partir dos algarismos digitados, descartando o que exceder o tipo.</summary>
    public static string Formatar(string texto, string tipo)
    {
        var algarismos = new string(texto.Where(char.IsAsciiDigit).Take(Algarismos(tipo)).ToArray());
        var barras = tipo == Data ? new[] { 2, 4 } : [2];
        var resultado = new System.Text.StringBuilder(algarismos.Length + barras.Length);
        for (var indice = 0; indice < algarismos.Length; indice++)
        {
            if (barras.Contains(indice))
                resultado.Append('/');
            resultado.Append(algarismos[indice]);
        }
        return resultado.ToString();
    }

    /// <summary>Converte um texto colado em outro formato comum para o formato do campo; nulo quando não reconhece.</summary>
    public static string? Normalizar(string texto, string tipo)
    {
        var limpo = texto.Trim();
        if (tipo == Data && DateTime.TryParseExact(limpo, FormatosData, Cultura, DateTimeStyles.None, out var data))
            return data.ToString("dd/MM/yyyy", Cultura);
        if (tipo == Competencia && DateTime.TryParseExact(limpo, FormatosCompetencia, Cultura, DateTimeStyles.None, out var competencia))
            return competencia.ToString("MM/yyyy", Cultura);
        return null;
    }

    private static void AoAlterarTipo(DependencyObject objeto, DependencyPropertyChangedEventArgs argumentos)
    {
        if (objeto is not TextBox campo)
            return;

        campo.PreviewTextInput -= FiltrarDigitacao;
        campo.TextChanged -= AplicarMascara;
        DataObject.RemovePastingHandler(campo, Colar);

        var algarismos = Algarismos(argumentos.NewValue as string);
        if (algarismos == 0)
        {
            campo.ClearValue(TextBox.MaxLengthProperty);
            campo.ClearValue(InputMethod.IsInputMethodEnabledProperty);
            return;
        }

        campo.MaxLength = algarismos + (algarismos == 8 ? 2 : 1);
        InputMethod.SetIsInputMethodEnabled(campo, false);
        campo.PreviewTextInput += FiltrarDigitacao;
        campo.TextChanged += AplicarMascara;
        DataObject.AddPastingHandler(campo, Colar);
    }

    private static void FiltrarDigitacao(object sender, TextCompositionEventArgs argumentos)
    {
        if (sender is not TextBox campo || GetTipo(campo) is not { } tipo)
            return;
        if (argumentos.Text.All(char.IsAsciiDigit))
        {
            // Com o campo completo e nada selecionado, o algarismo seria descartado pela máscara.
            argumentos.Handled = campo.SelectionLength == 0 && campo.Text.Count(char.IsAsciiDigit) >= Algarismos(tipo);
            return;
        }

        argumentos.Handled = true;
        if (argumentos.Text is not ("/" or "-" or ".") || campo.SelectionLength > 0 || campo.CaretIndex != campo.Text.Length)
            return;
        // "1/" vira "01/": completa o dia ou o mês digitado com um único algarismo.
        var segmento = campo.Text[(campo.Text.LastIndexOf('/') + 1)..];
        var barrasPossiveis = tipo == Data ? 2 : 1;
        if (segmento.Length == 1 && campo.Text.Count(caractere => caractere == '/') < barrasPossiveis)
        {
            campo.Text = Formatar(campo.Text[..^1] + "0" + segmento, tipo);
            campo.CaretIndex = campo.Text.Length;
        }
    }

    private static void Colar(object sender, DataObjectPastingEventArgs argumentos)
    {
        if (sender is not TextBox campo || GetTipo(campo) is not { } tipo
            || argumentos.DataObject.GetData(DataFormats.UnicodeText) is not string colado
            || Normalizar(colado, tipo) is not { } normalizado)
            return;
        argumentos.CancelCommand();
        campo.Text = normalizado;
        campo.CaretIndex = campo.Text.Length;
    }

    private static void AplicarMascara(object sender, TextChangedEventArgs argumentos)
    {
        // Só a edição do usuário é mascarada: valores carregados do histórico ou do ViewModel ficam como estão.
        if (_formatando || sender is not TextBox { IsKeyboardFocusWithin: true } campo || GetTipo(campo) is not { } tipo)
            return;

        var formatado = Formatar(campo.Text, tipo);
        if (formatado == campo.Text)
            return;

        var algarismosAntesDoCursor = campo.Text[..Math.Min(campo.CaretIndex, campo.Text.Length)].Count(char.IsAsciiDigit);
        _formatando = true;
        try
        {
            campo.Text = formatado;
            campo.CaretIndex = PosicaoAposAlgarismos(formatado, algarismosAntesDoCursor);
        }
        finally
        {
            _formatando = false;
        }
    }

    private static int PosicaoAposAlgarismos(string texto, int algarismos)
    {
        if (algarismos == 0)
            return 0;
        var contados = 0;
        for (var indice = 0; indice < texto.Length; indice++)
            if (char.IsAsciiDigit(texto[indice]) && ++contados == algarismos)
                return indice + 1;
        return texto.Length;
    }
}
