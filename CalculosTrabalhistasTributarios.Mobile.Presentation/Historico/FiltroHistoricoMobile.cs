using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;

/// <summary>Busca, filtros e ordem da lista de cálculos salvos, com os mesmos critérios do histórico do desktop.</summary>
public static class FiltroHistoricoMobile
{
    public const string TodasAsCalculadoras = "Todas as calculadoras";
    private static readonly CompareInfo Comparacao = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    public static IReadOnlyList<string> Periodos { get; } = ["Qualquer data", "Últimos 30 dias", "Este ano"];
    public static IReadOnlyList<string> Ordens { get; } = ["Mais recentes", "Mais antigos", "Nome"];

    /// <summary>"Todas as calculadoras" seguida dos nomes presentes nos cálculos salvos, em ordem alfabética.</summary>
    public static IReadOnlyList<string> Calculadoras(IEnumerable<CalculoSalvoDto> salvos) =>
    [
        TodasAsCalculadoras,
        .. salvos.Select(calculo => calculo.Calculadora).Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true))
    ];

    /// <param name="busca">Parte do nome dado ao cálculo ou do nome da calculadora, sem diferenciar acentos e maiúsculas.</param>
    /// <param name="periodo">Índice de <see cref="Periodos"/>.</param>
    /// <param name="ordem">Índice de <see cref="Ordens"/>.</param>
    public static IReadOnlyList<CalculoSalvoDto> Aplicar(IEnumerable<CalculoSalvoDto> salvos, string? busca, string? calculadora,
        int periodo, int ordem, DateTime hoje)
    {
        var texto = busca?.Trim() ?? string.Empty;
        var encontrados = salvos.Where(item =>
            (texto.Length == 0 || Contem(item.Nome, texto) || Contem(item.Calculadora, texto))
            && (calculadora is null or TodasAsCalculadoras || item.Calculadora == calculadora)
            && periodo switch
            {
                1 => item.AlteradoEm.Date >= hoje.Date.AddDays(-30),
                2 => item.AlteradoEm.Year == hoje.Year,
                _ => true
            });
        return (ordem switch
        {
            1 => encontrados.OrderBy(item => item.AlteradoEm),
            2 => encontrados.OrderBy(item => item.Nome, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true)),
            _ => encontrados.OrderByDescending(item => item.AlteradoEm)
        }).ToArray();
    }

    private static bool Contem(string texto, string busca) =>
        Comparacao.IndexOf(texto, busca, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
