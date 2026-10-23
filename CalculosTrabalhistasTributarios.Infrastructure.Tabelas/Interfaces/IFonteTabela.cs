using System.Net.Http;

namespace CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

/// <summary>Página na internet que publica uma tabela tributária.</summary>
public interface IFonteTabela<TTabela>
{
    /// <summary>Nome exibido ao usuário, como "debit.com.br".</summary>
    string Nome { get; }

    bool Oficial { get; }

    Uri Endereco { get; }

    /// <summary>Lê a tabela de vigência mais recente publicada na página.</summary>
    Task<TTabela> ObterAsync(HttpClient httpClient, CancellationToken cancellationToken);
}
