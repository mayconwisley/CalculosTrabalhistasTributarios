namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

/// <summary>Recursos do aparelho usados pelas telas: diálogos, navegador, tema e versão instalada.</summary>
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
}
