using System.Text;
using System.Text.Json;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;

namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence;

/// <summary>Arquivos privados e substituição atômica; não compartilha armazenamento com o histórico.</summary>
public sealed class ArquivoRascunhosFormularios(string diretorio) : IRascunhosFormularios, IDisposable
{
    public const int LimiteBytes = 4_000_000;
    private readonly SemaphoreSlim _acesso = new(1, 1);

    public async Task<RascunhoFormularioDto?> ObterAsync(string chave, CancellationToken cancellationToken)
    {
        var caminho = Caminho(chave);
        await _acesso.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(caminho)) return null;
            await using var arquivo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            if (arquivo.Length > LimiteBytes) throw new InvalidDataException("O rascunho excede o tamanho permitido.");
            var rascunho = await JsonSerializer.DeserializeAsync<RascunhoFormularioDto>(arquivo, cancellationToken: cancellationToken);
            if (rascunho is not { Versao: 1, Dados: not null }) throw new InvalidDataException("Formato de rascunho não suportado.");
            return rascunho;
        }
        finally { _acesso.Release(); }
    }

    public async Task SalvarAsync(string chave, RascunhoFormularioDto rascunho, CancellationToken cancellationToken)
    {
        var caminho = Caminho(chave);
        var json = JsonSerializer.Serialize(rascunho);
        if (rascunho.Versao != 1 || Encoding.UTF8.GetByteCount(json) > LimiteBytes)
            throw new InvalidDataException("O rascunho excede o limite ou tem formato incompatível.");
        await _acesso.WaitAsync(cancellationToken);
        var temporario = caminho + ".tmp";
        try
        {
            Directory.CreateDirectory(diretorio);
            await File.WriteAllTextAsync(temporario, json, Encoding.UTF8, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporario, caminho, true);
        }
        finally
        {
            try { if (File.Exists(temporario)) File.Delete(temporario); }
            finally { _acesso.Release(); }
        }
    }

    public async Task RemoverAsync(string chave, CancellationToken cancellationToken)
    {
        var caminho = Caminho(chave);
        await _acesso.WaitAsync(cancellationToken);
        try { File.Delete(caminho); }
        finally { _acesso.Release(); }
    }

    private string Caminho(string chave)
    {
        if (string.IsNullOrEmpty(chave) || chave.Length > 100 || chave.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-')))
            throw new ArgumentException("Chave de rascunho inválida.", nameof(chave));
        return Path.Combine(diretorio, chave + ".json");
    }

    public void Dispose() => _acesso.Dispose();
}
