namespace CalculosTrabalhistasTributarios.Infrastructure.Tributacao;

/// <param name="Fontes">Nomes das fontes que publicaram os valores escolhidos.</param>
internal sealed record TabelaEscolhida<T>(T Tabela, IReadOnlyList<string> Fontes, bool Oficial, IReadOnlyList<string> Observacoes);
