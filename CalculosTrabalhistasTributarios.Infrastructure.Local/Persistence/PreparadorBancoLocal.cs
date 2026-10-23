namespace CalculosTrabalhistasTributarios.Infrastructure.Persistence;

/// <summary>Copia a base distribuída de modo atômico, preserva o banco existente e aplica as sementes compartilhadas.</summary>
public sealed class PreparadorBancoLocal(string caminho, Func<CancellationToken, Task<Stream>> abrirBase,
    InicializadorBancoTributario inicializador, SqliteTributacaoConsulta consulta) : IDisposable
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private bool _preparado;

    public async Task PrepararAsync(CancellationToken cancellationToken)
    {
        await _trava.WaitAsync(cancellationToken);
        try
        {
            if (_preparado)
                return;
            if (!File.Exists(caminho))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(caminho) ?? throw new ArgumentException("Caminho do banco sem diretório."));
                var temporario = caminho + ".tmp";
                try
                {
                    await using var origem = await abrirBase(cancellationToken);
                    await using (var destino = File.Create(temporario))
                        await origem.CopyToAsync(destino, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Move(temporario, caminho);
                }
                finally
                {
                    File.Delete(temporario);
                }
            }
            await inicializador.InicializarAsync(cancellationToken);
            consulta.Invalidar();
            _preparado = true;
        }
        finally
        {
            _trava.Release();
        }
    }

    public void Dispose() => _trava.Dispose();
}
