using CalculosTrabalhistasTributarios.Domain.Comum;
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

    public async Task CompartilharConteudoAsync(string titulo, string nomeArquivo, string conteudo, string tipoConteudo)
    {
        var pasta = Path.Combine(FileSystem.Current.CacheDirectory, "sharing-root");
        var caminho = Path.Combine(pasta, Path.GetFileName(nomeArquivo));
        await Task.Run(() =>
        {
            Directory.CreateDirectory(pasta);
            File.WriteAllText(caminho, conteudo);
        });
        await Share.Default.RequestAsync(new ShareFileRequest(titulo, new ShareFile(caminho, tipoConteudo)));
    }

    // Arquivos recebidos pelo WhatsApp ou baixados do Drive costumam chegar como texto ou binário genérico, não como JSON.
    private static readonly FilePickerFileType TiposTexto = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.Android] = ["application/json", "text/plain", "application/octet-stream"]
    });

    public async Task<Result<string>?> EscolherArquivoTextoAsync(string titulo, int limiteCaracteres)
    {
        var arquivo = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = titulo, FileTypes = TiposTexto });
        if (arquivo is null) return null;
        try
        {
            await using var stream = await arquivo.OpenReadAsync();
            return await Task.Run(async () =>
            {
                using var leitor = new StreamReader(stream);
                var texto = new System.Text.StringBuilder();
                var buffer = new char[64 * 1024];
                int lidos;
                // Lê em partes e para no limite, sem carregar um arquivo enorme inteiro na memória.
                while ((lidos = await leitor.ReadAsync(buffer)) > 0)
                {
                    if (texto.Length + lidos > limiteCaracteres)
                        return Result<string>.Falha(Erro.Validacao($"O arquivo “{arquivo.FileName}” é grande demais para uma cópia de segurança do histórico."));
                    texto.Append(buffer, 0, lidos);
                }
                return Result<string>.Ok(texto.ToString());
            });
        }
        catch (IOException)
        {
            return Result<string>.Falha(Erro.Indisponivel($"Não foi possível ler o arquivo “{arquivo.FileName}”. Copie-o para o aparelho e tente novamente."));
        }
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
