namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Endereco">Página a abrir na versão nova; nulo no aviso das tabelas, que abre a manutenção.</param>
public sealed record AvisoAtualizacaoDto(TipoAvisoAtualizacao Tipo, string Mensagem, Uri? Endereco);
