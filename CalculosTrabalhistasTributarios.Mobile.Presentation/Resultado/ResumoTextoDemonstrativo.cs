using System.Globalization;
using System.Text;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Resultado;

/// <summary>
/// Resumo em texto simples para colar numa conversa: destaques, comparativo e totais, sem a memória, que fica no PDF.
/// Usa os mesmos valores já apurados no demonstrativo e termina com o aviso de simulação, como o rodapé do PDF.
/// </summary>
public static class ResumoTextoDemonstrativo
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static string Montar(DemonstrativoDto demonstrativo)
    {
        var texto = new StringBuilder();
        texto.AppendLine(demonstrativo.Titulo);
        if (!string.IsNullOrWhiteSpace(demonstrativo.Referencia)) texto.AppendLine(demonstrativo.Referencia);

        if (demonstrativo.Destaques.Count > 0)
        {
            texto.AppendLine();
            foreach (var destaque in demonstrativo.Destaques)
                texto.AppendLine(string.IsNullOrWhiteSpace(destaque.Complemento)
                    ? $"{destaque.Rotulo}: {destaque.Valor}"
                    : $"{destaque.Rotulo}: {destaque.Valor} ({destaque.Complemento})");
        }

        if (demonstrativo.Comparativo is { } comparativo)
        {
            texto.AppendLine().AppendLine(comparativo.Titulo);
            foreach (var linha in comparativo.Linhas)
            {
                var valores = linha.Valores.Select((valor, indice) =>
                    indice < comparativo.Colunas.Count ? $"{comparativo.Colunas[indice]} {valor}" : valor);
                texto.AppendLine($"{linha.Descricao}: {string.Join(" | ", valores)}");
            }
        }

        if (demonstrativo.TemVerbas)
        {
            texto.AppendLine()
                .AppendLine($"{demonstrativo.RotuloProventos}: {Moeda(demonstrativo.TotalProventos)}")
                .AppendLine($"Descontos: {Moeda(demonstrativo.TotalDescontos)}")
                .AppendLine($"{demonstrativo.RotuloResultado}: {Moeda(demonstrativo.Resultado)}");
        }

        texto.AppendLine().AppendLine(AvisoSimulacao.Titulo).Append(AvisoSimulacao.Orientacao);
        return texto.ToString();
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", Cultura);
}
