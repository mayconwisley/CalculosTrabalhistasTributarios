namespace CalculosTrabalhistasTributarios.Infrastructure.Arquivos;

/// <summary>Manutenção oportunista; devolve a quantidade de falhas de acesso sem impedir uma nova exportação.</summary>
public static class LimpezaCacheCompartilhamento
{
    public static int Executar(string pasta, DateTime agoraUtc, CancellationToken cancellationToken)
    {
        var falhas = 0;
        try
        {
            foreach (var arquivo in Directory.EnumerateFiles(pasta, "simulacao-*.pdf"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (File.GetLastWriteTimeUtc(arquivo) < agoraUtc.AddDays(-7)) File.Delete(arquivo);
                }
                catch (IOException) { falhas++; }
                catch (UnauthorizedAccessException) { falhas++; }
            }
        }
        catch (IOException) { falhas++; }
        catch (UnauthorizedAccessException) { falhas++; }
        return falhas;
    }
}
