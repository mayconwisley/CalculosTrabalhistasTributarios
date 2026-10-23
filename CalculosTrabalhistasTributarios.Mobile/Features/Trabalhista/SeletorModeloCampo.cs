using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

namespace CalculosTrabalhistasTributarios.Mobile.Features.Trabalhista;

/// <summary>
/// Cria só o controle do tipo do campo. Um modelo único com todas as variantes ocultas multiplica os controles nativos e
/// torna lenta a geração de grades longas, que não são virtualizadas para não prender a rolagem da página.
/// </summary>
public sealed class SeletorModeloCampo : DataTemplateSelector
{
    public DataTemplate? Secao { get; set; }
    public DataTemplate? Texto { get; set; }
    public DataTemplate? Moeda { get; set; }
    public DataTemplate? Opcao { get; set; }
    public DataTemplate? SimNao { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        var campo = (CampoMobile)item;
        var modelo = campo.EhSecao ? Secao : campo.EhMoeda ? Moeda : campo.EhOpcao ? Opcao : campo.EhSimNao ? SimNao : Texto;
        return modelo ?? throw new InvalidOperationException($"Modelo ausente para o campo do tipo {campo.Tipo}.");
    }
}
