using System.Collections.ObjectModel;
using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>Projeção para leitura paginada. Não calcula verbas nem altera o demonstrativo exportado.</summary>
public sealed class DetalhesResultadoMobile(DemonstrativoDto demonstrativo)
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    public PaginaLinhas<VerbaDto> Proventos => field ??= new(new ObservableCollection<VerbaDto>(demonstrativo.Proventos));
    public PaginaLinhas<VerbaDto> Descontos => field ??= new(new ObservableCollection<VerbaDto>(demonstrativo.Descontos));
    public PaginaLinhas<VerbaDto> Informativos => field ??= new(new ObservableCollection<VerbaDto>(demonstrativo.Informativos));
    public PaginaLinhas<LinhaMemoriaMobile> Memoria => field ??= new(new ObservableCollection<LinhaMemoriaMobile>(LerMemoria()));
    public PaginaLinhas<LinhaMemoriaMobile> Comparativo => field ??= new(new ObservableCollection<LinhaMemoriaMobile>(LerComparativo()));

    private IEnumerable<LinhaMemoriaMobile> LerComparativo()
    {
        if (demonstrativo.Comparativo is not { } quadro) yield break;
        foreach (var linha in quadro.Linhas)
            yield return new(linha.Descricao, string.Join("\n", quadro.Colunas.Zip(linha.Valores, (coluna, valor) => $"{coluna}: {valor}")));
    }
    private IEnumerable<LinhaMemoriaMobile> LerMemoria()
    {
        foreach (var grupo in demonstrativo.Memoria)
        {
            yield return new(grupo.Titulo, grupo.Destaque);
            if (grupo.Quadro is { } quadro)
                foreach (var faixa in quadro.Faixas)
                    yield return new($"{grupo.Titulo} — Faixa {faixa.Numero}", string.Join("\n",
                        $"De: {faixa.De.ToString("C2", Cultura)}; até: {(faixa.Ate is { } ate ? ate.ToString("C2", Cultura) : "sem limite")}",
                        $"Base nesta faixa: {faixa.ParteDaBase.ToString("C2", Cultura)}; alíquota: {faixa.Aliquota.ToString("0.##", Cultura)}%",
                        $"Valor exato: {faixa.ValorExato.ToString("0.00####", Cultura)}; considerado: {faixa.Valor.ToString("0.00####", Cultura)}"));
            foreach (var formula in grupo.Formulas) yield return new($"{grupo.Titulo} — {formula.Titulo}", formula.Formula);
        }
    }
}
