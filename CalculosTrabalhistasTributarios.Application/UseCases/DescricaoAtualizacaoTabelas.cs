using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>Textos do resultado da atualização das tabelas, os mesmos no desktop e no celular.</summary>
public static class DescricaoAtualizacaoTabelas
{
    /// <summary>Texto do aviso quando alguma tabela não foi atualizada; nulo quando todas foram.</summary>
    public static string? DescreverPendencias(IReadOnlyList<AtualizacaoDeTabela> resultados)
    {
        var falhas = resultados.Where(resultado => resultado.Resultado.Falhou).ToList();
        if (falhas.Count == 0)
            return null;
        if (falhas.Count == resultados.Count)
            return "As tabelas não foram atualizadas: nenhuma fonte respondeu. Confira a conexão com a internet; os dados locais foram preservados e a atualização será tentada de novo na próxima abertura.";
        return $"Na última atualização, {falhas.Count} de {resultados.Count} tabelas não foram atualizadas ({string.Join(", ", falhas.Select(falha => falha.Nome))}). " +
            "Os dados locais foram preservados; atualize-as pela tela de cada tabela.";
    }

    /// <summary>O motivo informado pela fonte de cada tabela não atualizada.</summary>
    public static string DetalharPendencias(IReadOnlyList<AtualizacaoDeTabela> resultados) => string.Join("\n\n",
        resultados.Where(resultado => resultado.Resultado.Falhou).Select(falha => $"{falha.Nome}: {falha.Resultado.Erro.Mensagem}"));

    /// <summary>Resultado da atualização de uma tabela pela tela da própria tabela.</summary>
    public static string DescreverAtualizacao(TipoTabelaTributaria tipo, AtualizacaoTabelaResultado resultado)
    {
        string texto;
        if (EhIndice(tipo))
        {
            var meses = resultado.QuantidadeFaixas == 1 ? "1 mês incluído ou corrigido" : $"{resultado.QuantidadeFaixas} meses incluídos ou corrigidos";
            texto = $"Índices até {resultado.Competencia:MM/yyyy} conferidos com {string.Join(" e ", resultado.Fontes)} ({meses}).";
        }
        else
        {
            var origem = resultado.Oficial ? "pela fonte oficial" : $"por {string.Join(" e ", resultado.Fontes)}, que já publicaram a tabela";
            texto = resultado.QuantidadeFaixas switch
            {
                // O desconto mínimo é só conferido: quando o banco já tem o valor da lei, nada é gravado.
                0 => $"Dados de {resultado.Competencia:MM/yyyy} conferidos {origem}: os valores cadastrados já estavam corretos.",
                1 => $"Dados de {resultado.Competencia:MM/yyyy} atualizados {origem} (1 valor importado).",
                var faixas => $"Dados de {resultado.Competencia:MM/yyyy} atualizados {origem} ({faixas} faixas tributárias importadas)."
            };
        }
        return resultado.Observacoes.Count == 0 ? texto : $"{texto} {string.Join(" ", resultado.Observacoes)}";
    }

    /// <summary>Índices mensais: a variação da inflação pode ser negativa, e a atualização importa meses, não faixas.</summary>
    public static bool EhIndice(TipoTabelaTributaria tipo) => tipo is TipoTabelaTributaria.Inpc or TipoTabelaTributaria.Ipca or TipoTabelaTributaria.TaxaLegal
        or TipoTabelaTributaria.Selic or TipoTabelaTributaria.IpcaE or TipoTabelaTributaria.Tr;
}
