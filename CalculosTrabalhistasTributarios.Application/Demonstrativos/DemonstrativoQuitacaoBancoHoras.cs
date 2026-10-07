using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos;

internal static class DemonstrativoQuitacaoBancoHoras
{
    public static IEnumerable<VerbaDto> Proventos(QuitacaoBancoHoras quitacao) => quitacao.Parcelas.Select(parcela =>
        new VerbaDto($"Quitação do banco de horas ({Formato.PercentualCurto(parcela.Adicional)})",
            Horas(parcela.Minutos), parcela.Valor));

    public static GrupoMemoriaDto Memoria(QuitacaoBancoHoras quitacao) => new("Quitação do banco de horas",
        $"{Formato.Moeda(quitacao.Total)} em {quitacao.Parcelas.Count} faixa(s) de adicional",
        [
            new("Ciclo e base", $"Fim em {Formato.Data(quitacao.FimCiclo)}; salário de referência {Formato.Moeda(quitacao.SalarioReferencia)}; divisor {Formato.Numero(quitacao.Divisor)}"),
            .. quitacao.Parcelas.Select(parcela => new FormulaDto($"Adicional de {Formato.PercentualCurto(parcela.Adicional)}",
                $"{parcela.Minutos} min × {Formato.Moeda(quitacao.SalarioReferencia)} × (1 + {Formato.PercentualCurto(parcela.Adicional)}) ÷ ({Formato.Numero(quitacao.Divisor)} × 60) = {Formato.Moeda(parcela.Valor)}")),
            new("Incidências", $"{Formato.Moeda(quitacao.Total)} integra as bases do INSS, IRRF e FGTS; sem DSR ou reflexos calculados automaticamente")
        ]);

    public const string Observacao = "As parcelas de quitação do banco de horas foram incluídas como horas extras não compensadas, com INSS, IRRF e FGTS (rubrica eSocial1120). Não as repita em horas extras ou outros proventos. O banco não calcula DSR nem reflexos; confira a norma coletiva e a remuneração da data da quitação.";

    private static string Horas(int minutos) => $"{minutos / 60}:{minutos % 60:00}";
}
