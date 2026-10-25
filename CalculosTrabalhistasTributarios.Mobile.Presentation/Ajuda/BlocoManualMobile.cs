namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Ajuda;

/// <summary>Trecho do manual pronto para exibir. O texto mantém o negrito em **asteriscos**; links e código já vêm sem marcação.</summary>
/// <param name="Arquivo">Na imagem, o arquivo empacotado no aplicativo (ManualMobile.PastaImagens); o texto é a descrição.</param>
public sealed record BlocoManualMobile(TipoBlocoManualMobile Tipo, string Texto, string? Arquivo = null);
