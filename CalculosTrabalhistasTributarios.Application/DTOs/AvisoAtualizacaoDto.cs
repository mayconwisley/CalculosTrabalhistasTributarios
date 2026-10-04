namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Endereco">Página a abrir na versão nova; nulo no aviso das tabelas, que abre a manutenção.</param>
/// <param name="Versao">A versão publicada, no aviso de versão nova, para baixar o instalador pelo aplicativo.</param>
public sealed record AvisoAtualizacaoDto(TipoAvisoAtualizacao Tipo, string Mensagem, Uri? Endereco, VersaoPublicada? Versao = null);
