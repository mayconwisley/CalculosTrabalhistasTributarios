using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace CalculosTrabalhistasTributarios.Infrastructure.Atualizacao;

/// <summary>
/// Baixa o instalador para a pasta temporária e confere o SHA-256 publicado com o release. Só aceita arquivos dos
/// releases deste repositório no GitHub: um endereço de outro lugar, ou um arquivo diferente do publicado, nunca é executado.
/// </summary>
public sealed class BaixadorDeAtualizacaoGitHub(Func<HttpClient> criarHttpClient) : IBaixadorDeAtualizacao
{
    private static readonly string PrefixoDosReleases = $"/{ConsultaVersaoGitHub.Repositorio}/releases/download/";
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "CalculosTrabalhistasTributarios");

    public async Task<Result<string>> BaixarAsync(VersaoPublicada versao, IProgress<int>? progresso, CancellationToken cancellationToken)
    {
        if (versao.Instalador is not { } endereco || versao.Sha256 is not { } esperado)
            return Erro.Indisponivel("Este release não tem o instalador com o hash para conferência; baixe-o pela página do GitHub.");
        if (endereco.Scheme != Uri.UriSchemeHttps || endereco.Host != "github.com" || !endereco.AbsolutePath.StartsWith(PrefixoDosReleases, StringComparison.Ordinal))
            return Erro.Indisponivel("O endereço do instalador não é de um release do projeto no GitHub; a atualização foi cancelada.");

        var arquivo = Path.Combine(_pasta, Path.GetFileName(endereco.AbsolutePath));
        try
        {
            Directory.CreateDirectory(_pasta);
            using var httpClient = criarHttpClient();
            // Só até os cabeçalhos: o tempo limite do cliente não pode interromper um download longo numa conexão lenta.
            using var resposta = await httpClient.GetAsync(endereco, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            resposta.EnsureSuccessStatusCode();
            var tamanho = resposta.Content.Headers.ContentLength;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var origem = await resposta.Content.ReadAsStreamAsync(cancellationToken))
            await using (var destino = File.Create(arquivo))
            {
                var buffer = new byte[81_920];
                long lidos = 0;
                int quantidade;
                while ((quantidade = await origem.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await destino.WriteAsync(buffer.AsMemory(0, quantidade), cancellationToken);
                    hash.AppendData(buffer, 0, quantidade);
                    lidos += quantidade;
                    if (tamanho > 0)
                        progresso?.Report((int)(lidos * 100 / tamanho.Value));
                }
            }

            if (!string.Equals(Convert.ToHexStringLower(hash.GetHashAndReset()), esperado, StringComparison.OrdinalIgnoreCase))
            {
                Descartar(arquivo);
                return Erro.Indisponivel("O instalador baixado não confere com o publicado no GitHub e foi descartado. Tente de novo ou baixe-o pela página do release.");
            }
            return arquivo;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException or TaskCanceledException)
        {
            Descartar(arquivo);
            return Erro.Indisponivel("Não foi possível baixar o instalador. Verifique a conexão com a internet e tente de novo.");
        }
    }

    // Um arquivo incompleto ou adulterado não pode ficar na pasta; se não der para apagar, ele nunca é executado de qualquer jeito.
    private static void Descartar(string arquivo)
    {
        try
        {
            File.Delete(arquivo);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
