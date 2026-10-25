using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;

/// <summary>
/// A comparação de dois cálculos salvos no formato do demonstrativo, para gerar o PDF com o mesmo gerador das calculadoras.
/// Só há o quadro comparativo: proventos e descontos de cada cálculo ficam no PDF de cada um.
/// </summary>
public static class DemonstrativoComparacaoHistorico
{
    public const string ObservacaoRecalculo =
        "Os resultados foram recalculados com as tabelas locais atuais para cada competência. Diferença = segundo menos primeiro.";

    public static DemonstrativoDto Montar(string primeiro, string segundo, IReadOnlyList<LinhaResultadoComparadoViewModel> linhas, DateTime geradoEm) =>
        new("Comparação de cálculos salvos", $"Primeiro: {primeiro} · Segundo: {segundo}", [], [], [], [], [],
            [ObservacaoRecalculo, $"Comparação gerada em {geradoEm:dd/MM/yyyy HH:mm}."],
            Comparativo: new TabelaComparativaDto("Resultados comparados", ["Primeiro", "Segundo", "Diferença"],
                [.. linhas.Select(linha => new LinhaComparativaDto($"{linha.Grupo} · {linha.Item}",
                    [linha.Primeiro, linha.Segundo, linha.Diferenca], linha.Diferente))]));
}
