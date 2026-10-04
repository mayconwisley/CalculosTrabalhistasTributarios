using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Interfaces;
using CalculosTrabalhistasTributarios.Infrastructure.Persistence;
using CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <summary>
/// Importa as séries mensais do INPC, do IPCA, do IPCA-E, da taxa legal, da Selic e da TR. A fonte oficial grava todos os meses publicados, corrigindo os
/// que estiverem diferentes; se ela não responder, a fonte alternativa só preenche os meses que ainda faltam, sem alterar
/// os cadastrados.
/// </summary>
public sealed class AtualizadorIndices(Func<HttpClient> criarHttpClient, IEnumerable<IFonteIndice> fontes, BancoTributario banco)
{
    /// <summary>Primeiro mês das séries, o mesmo das sementes.</summary>
    private static readonly DateOnly Inicio = new(2015, 1, 1);

    private readonly IFonteIndice[] _fontes = fontes.ToArray();

    public Uri FonteOficial(TipoTabelaTributaria tabela) => Fontes(tabela).First(fonte => fonte.Oficial).Endereco;

    public string NomeFonteOficial(TipoTabelaTributaria tabela) => Fontes(tabela).First(fonte => fonte.Oficial).Nome;

    public async Task<AtualizacaoTabelaResultado> AtualizarAsync(TipoTabelaTributaria tabela, CancellationToken cancellationToken)
    {
        var falhas = new List<string>();
        foreach (var fonte in Fontes(tabela).OrderByDescending(fonte => fonte.Oficial))
        {
            IReadOnlyList<ValorMensalPublicado> valores;
            try
            {
                using var httpClient = criarHttpClient();
                valores = Validar(await fonte.ObterAsync(httpClient, Inicio, cancellationToken), fonte);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                falhas.Add($"{fonte.Nome}: {exception.Message}");
                continue;
            }

            var gravados = await GravarAsync(tabela, valores, corrigirExistentes: fonte.Oficial, cancellationToken);
            var observacoes = new List<string>();
            if (!fonte.Oficial)
                observacoes.Add($"A fonte oficial não respondeu ({string.Join("; ", falhas)}); só os meses que faltavam foram preenchidos com os valores de {fonte.Nome}. Atualize de novo mais tarde para conferir na fonte oficial.");
            if (gravados == 0)
                observacoes.Add("Nenhum mês novo ou diferente: a tabela já estava atualizada.");
            return new AtualizacaoTabelaResultado(valores.Max(valor => valor.Competencia), gravados, [fonte.Nome], fonte.Oficial, observacoes);
        }

        throw new InvalidOperationException($"Nenhuma fonte respondeu. {string.Join(" ", falhas.Select(falha => falha.EndsWith('.') ? falha : falha + "."))}");
    }

    private IEnumerable<IFonteIndice> Fontes(TipoTabelaTributaria tabela) => _fontes.Where(fonte => fonte.Tabela == tabela);

    // Uma página mudada de formato não pode gravar lixo: a série precisa ter meses e valores plausíveis para um mês.
    // Nenhum mês posterior ao atual: a taxa legal do mês sai no início dele, mas a de um mês futuro seria erro da página.
    private static IReadOnlyList<ValorMensalPublicado> Validar(IReadOnlyList<ValorMensalPublicado> publicados, IFonteIndice fonte)
    {
        var mesAtual = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var valores = publicados.Where(valor => valor.Competencia <= mesAtual).ToList();
        if (valores.Count == 0)
            throw new InvalidOperationException("nenhum mês encontrado na página");
        if (valores.Any(valor => valor.Valor is < -10m or > 10m))
            throw new InvalidOperationException("valores fora do esperado para um mês; a página pode ter mudado de formato");
        if (fonte.Tabela is TipoTabelaTributaria.TaxaLegal or TipoTabelaTributaria.Selic or TipoTabelaTributaria.Tr && valores.Any(valor => valor.Valor < 0m))
            throw new InvalidOperationException("uma taxa de juros não pode ser negativa; a página pode ter mudado de formato");
        return valores;
    }

    /// <returns>Quantidade de meses incluídos ou corrigidos.</returns>
    private async Task<int> GravarAsync(TipoTabelaTributaria tabela, IReadOnlyList<ValorMensalPublicado> valores, bool corrigirExistentes, CancellationToken cancellationToken)
    {
        var nome = tabela switch
        {
            TipoTabelaTributaria.Inpc => "Inpc",
            TipoTabelaTributaria.Ipca => "Ipca",
            TipoTabelaTributaria.IpcaE => "IpcaE",
            TipoTabelaTributaria.Selic => "Selic",
            TipoTabelaTributaria.Tr => "Tr",
            _ => "TaxaLegal"
        };
        await using var conexao = await banco.AbrirAsync(cancellationToken);
        await using var transacao = conexao.BeginTransaction();
        var existentes = (await conexao.ListarAsync(transacao, $"SELECT Id, Competencia, Valor FROM {nome}",
                leitor => (Id: leitor.GetInt32(0), Competencia: DateOnly.FromDateTime(leitor.GetDateTime(1)), Valor: leitor.GetDecimal(2)), cancellationToken))
            .GroupBy(registro => new DateOnly(registro.Competencia.Year, registro.Competencia.Month, 1))
            .ToDictionary(grupo => grupo.Key, grupo => grupo.MaxBy(registro => registro.Id));

        var gravados = 0;
        foreach (var valor in valores.DistinctBy(valor => valor.Competencia))
        {
            if (!existentes.TryGetValue(valor.Competencia, out var existente))
            {
                await conexao.ExecutarAsync(transacao, $"INSERT INTO {nome} (Competencia, Valor) VALUES ($competencia, $valor)", cancellationToken,
                    ("$competencia", valor.Competencia.ToDateTime(TimeOnly.MinValue)), ("$valor", (double)valor.Valor));
                gravados++;
            }
            else if (corrigirExistentes && Math.Round(existente.Valor, 4) != Math.Round(valor.Valor, 4))
            {
                await conexao.ExecutarAsync(transacao, $"UPDATE {nome} SET Valor = $valor WHERE Id = $id", cancellationToken,
                    ("$valor", (double)valor.Valor), ("$id", existente.Id));
                gravados++;
            }
        }
        await transacao.CommitAsync(cancellationToken);
        return gravados;
    }
}
