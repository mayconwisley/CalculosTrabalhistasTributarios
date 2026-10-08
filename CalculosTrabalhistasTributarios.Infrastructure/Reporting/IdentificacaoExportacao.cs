using System.Reflection;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting;

internal static class IdentificacaoExportacao
{
    internal static string Versao
    {
        get
        {
            var executavel = Assembly.GetEntryAssembly();
            var assembly = executavel?.GetName().Name == "CalculosTrabalhistasTributarios"
                ? executavel
                : typeof(IdentificacaoExportacao).Assembly;
            var versao = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString(3)
                ?? "não informada";
            var indiceCommit = versao.IndexOf('+');
            return indiceCommit >= 0 ? versao[..indiceCommit] : versao;
        }
    }
}
