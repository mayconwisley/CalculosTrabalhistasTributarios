using CalculosTrabalhistasTributarios.Application.Interfaces;
using System.IO;
using System.Text;

namespace CalculosTrabalhistasTributarios.Infrastructure.Diagnostico;

/// <summary>
/// Registra os erros inesperados num arquivo de texto por mês, na pasta das preferências do usuário, que pode ser gravada
/// mesmo quando a pasta do aplicativo não pode. Arquivos de mais de um ano são apagados.
/// </summary>
public sealed class ArquivoRegistroDeErros : IRegistroDeErros
{
    private const int MesesGuardados = 12;
    // Uma instância é criada antes da injeção de dependências, para registrar falhas na própria inicialização.
    private static readonly Lock Trava = new();
    private readonly string _pasta;
    private readonly string _versao;

    public ArquivoRegistroDeErros() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CalculoIRRF", "logs"))
    {
    }

    public ArquivoRegistroDeErros(string pasta)
    {
        _pasta = pasta;
        var versao = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        _versao = versao is null ? "desconhecida" : versao.ToString(3);
    }

    public string Arquivo => Path.Combine(_pasta, $"erros-{DateTime.Now:yyyy-MM}.log");

    public void Registrar(Exception exception, string contexto)
    {
        try
        {
            var texto = new StringBuilder()
                .AppendLine($"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {contexto}")
                .AppendLine($"Versão {_versao} • {Environment.OSVersion}")
                .AppendLine(exception.ToString())
                .AppendLine()
                .ToString();
            lock (Trava)
            {
                Directory.CreateDirectory(_pasta);
                File.AppendAllText(Arquivo, texto, Encoding.UTF8);
                ApagarAntigos();
            }
        }
        catch (Exception falha) when (falha is IOException or UnauthorizedAccessException)
        {
            // Sem onde gravar, o erro ainda é mostrado ao usuário; não há o que fazer além disso.
        }
    }

    private void ApagarAntigos()
    {
        var limite = DateTime.Now.AddMonths(-MesesGuardados);
        foreach (var arquivo in Directory.EnumerateFiles(_pasta, "erros-*.log").Where(arquivo => File.GetLastWriteTime(arquivo) < limite))
            File.Delete(arquivo);
    }
}
