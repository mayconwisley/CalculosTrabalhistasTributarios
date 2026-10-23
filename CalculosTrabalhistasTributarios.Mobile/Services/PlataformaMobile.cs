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

    // Uma cópia na pasta do provedor de arquivos do app, a mesma do PDF: o destinatário não lê o armazenamento privado.
    public async Task CompartilharArquivoAsync(string titulo, string caminho)
    {
        var pasta = Path.Combine(FileSystem.Current.CacheDirectory, "sharing-root");
        var copia = Path.Combine(pasta, Path.GetFileName(caminho));
        await Task.Run(() =>
        {
            Directory.CreateDirectory(pasta);
            File.Copy(caminho, copia, overwrite: true);
        });
        await Share.Default.RequestAsync(new ShareFileRequest(titulo, new ShareFile(copia, "text/plain")));
    }

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
