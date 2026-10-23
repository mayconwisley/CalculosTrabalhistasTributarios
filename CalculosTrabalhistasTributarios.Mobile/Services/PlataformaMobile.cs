using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

public sealed class PlataformaMobile : IPlataformaMobile
{
    public string Versao => AppInfo.Current.VersionString;

    private static Page Pagina => Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("Janela do aplicativo indisponível.");

    public Task MostrarAsync(string titulo, string mensagem) => Pagina.DisplayAlertAsync(titulo, mensagem, "Entendi");

    public Task<bool> ConfirmarAsync(string titulo, string mensagem, string aceitar, string cancelar) =>
        Pagina.DisplayAlertAsync(titulo, mensagem, aceitar, cancelar);

    public Task AbrirEnderecoAsync(Uri endereco) => Browser.Default.OpenAsync(endereco, BrowserLaunchMode.SystemPreferred);

    public void AplicarTema(TemaMobile tema)
    {
        if (Microsoft.Maui.Controls.Application.Current is { } app)
            app.UserAppTheme = tema switch
            {
                TemaMobile.Claro => AppTheme.Light,
                TemaMobile.Escuro => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
    }
}
