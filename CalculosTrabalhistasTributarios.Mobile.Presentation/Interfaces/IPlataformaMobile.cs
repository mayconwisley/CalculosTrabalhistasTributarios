using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

/// <summary>Recursos do aparelho usados pelas telas: diálogos, navegador, tema, arquivos e versão instalada.</summary>
public interface IPlataformaMobile
{
    string Versao { get; }
    Task MostrarAsync(string titulo, string mensagem);
    Task<bool> ConfirmarAsync(string titulo, string mensagem, string aceitar, string cancelar);

    /// <summary>Abre a página no navegador do aparelho; nenhum dado do formulário é enviado.</summary>
    Task AbrirEnderecoAsync(Uri endereco);
    void AplicarTema(TemaMobile tema);

    /// <summary>Abre a folha de compartilhamento do aparelho com o arquivo; o usuário escolhe o destino.</summary>
    Task CompartilharArquivoAsync(string titulo, string caminho);

    /// <summary>Grava o conteúdo num arquivo temporário com o nome indicado e abre a folha de compartilhamento.</summary>
    Task CompartilharConteudoAsync(string titulo, string nomeArquivo, string conteudo, string tipoConteudo);

    /// <summary>
    /// Abre o seletor de arquivos do aparelho e lê o texto escolhido. Nulo quando o usuário cancela; falha quando o arquivo
    /// passa de <paramref name="limiteCaracteres"/> ou não pode ser lido.
    /// </summary>
    Task<Result<string>?> EscolherArquivoTextoAsync(string titulo, int limiteCaracteres);
}
