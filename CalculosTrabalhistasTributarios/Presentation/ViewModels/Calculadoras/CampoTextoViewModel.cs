using System.Windows;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CampoTextoViewModel(string rotulo, TipoCampo tipo, string valorInicial, string? dica = null, bool permiteVazio = false) : CampoViewModel(rotulo, dica)
{
    private string _valor = valorInicial;

    public TipoCampo Tipo { get; } = tipo;
    public string Valor { get => _valor; set => SetProperty(ref _valor, value); }

    /// <summary>Formato aplicado ao sair do campo; só os valores monetários são reformatados.</summary>
    // Campos opcionais preservam a diferença entre vazio e zero, sem conversão automática ao perder foco.
    public string? Formato => Tipo == TipoCampo.Moeda && !permiteVazio ? "N2" : null;

    /// <summary>Zero exibido quando o campo é deixado vazio; um campo zerado é limpo ao receber o foco.</summary>
    public string? ValorVazio => Tipo switch
    {
        TipoCampo.Inteiro or TipoCampo.Numero => "0",
        TipoCampo.Horas => "0:00",
        _ => null
    };

    public TextAlignment Alinhamento => Tipo is TipoCampo.Moeda or TipoCampo.Numero or TipoCampo.Inteiro or TipoCampo.Horas ? TextAlignment.Right : TextAlignment.Center;
}
