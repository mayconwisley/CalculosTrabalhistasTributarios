namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Numero">Versão sem o "v" da tag, como "1.6.1".</param>
/// <param name="Endereco">Página do release, com o instalador.</param>
public sealed record VersaoPublicada(string Numero, Uri Endereco);
