namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Numero">Versão sem o "v" da tag, como "1.6.1".</param>
/// <param name="Endereco">Página do release, com as novidades.</param>
/// <param name="Instalador">Endereço de download do instalador; nulo quando o release não tem um.</param>
/// <param name="Sha256">Hash do instalador em hexadecimal, conferido depois do download; nulo quando não foi publicado.</param>
public sealed record VersaoPublicada(string Numero, Uri Endereco, Uri? Instalador = null, string? Sha256 = null)
{
    /// <summary>Só se instala pelo aplicativo o que pode ser conferido depois do download.</summary>
    public bool PodeAtualizarPeloAplicativo => Instalador is not null && Sha256 is not null;
}
