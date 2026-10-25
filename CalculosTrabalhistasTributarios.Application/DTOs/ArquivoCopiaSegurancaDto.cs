namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Cópia de segurança pronta para gravar ou compartilhar.</summary>
public sealed record ArquivoCopiaSegurancaDto(string NomeArquivo, string Conteudo, int Quantidade);
