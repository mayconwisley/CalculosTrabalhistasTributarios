using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Abertura;

/// <summary>Resultado da última atualização feita nesta execução, pela abertura ou por Atualizar agora, para o aviso da tela inicial.</summary>
public sealed class SituacaoAtualizacaoTabelas
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public IReadOnlyList<AtualizacaoDeTabela> Resultados { get; private set; } = [];

    public event EventHandler? Alterada;

    public void Informar(IReadOnlyList<AtualizacaoDeTabela> resultados)
    {
        Resultados = resultados;
        Alterada?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Texto de quando as tabelas foram atualizadas pela última vez.</summary>
    public static string DescreverUltimaAtualizacao(DateTime? horario, DateTime agora)
    {
        if (horario is not { } quando)
            return "As tabelas ainda não foram atualizadas pela internet neste aparelho; os cálculos usam as tabelas distribuídas com o aplicativo.";
        var dia = DateOnly.FromDateTime(quando);
        var hoje = DateOnly.FromDateTime(agora);
        var texto = dia == hoje ? "hoje" : dia == hoje.AddDays(-1) ? "ontem" : $"em {quando.ToString("dd/MM/yyyy", Cultura)}";
        return $"Tabelas atualizadas pela internet {texto} às {quando.ToString("HH:mm", Cultura)}.";
    }
}
