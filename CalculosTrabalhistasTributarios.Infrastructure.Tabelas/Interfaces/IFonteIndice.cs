using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Infrastructure.Tributacao.Fontes;
using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

/// <summary>Uma fonte da série mensal de um índice econômico.</summary>
public interface IFonteIndice
{
    TipoTabelaTributaria Tabela { get; }
    string Nome { get; }
    bool Oficial { get; }

    /// <summary>Página da série para o usuário conferir os valores.</summary>
    Uri Endereco { get; }

    Task<IReadOnlyList<ValorMensalPublicado>> ObterAsync(HttpClient httpClient, DateOnly inicio, CancellationToken cancellationToken);
}
