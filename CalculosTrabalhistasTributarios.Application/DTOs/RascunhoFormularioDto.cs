namespace CalculosTrabalhistasTributarios.Application.DTOs;

public sealed record RascunhoFormularioDto(int Versao, string Dados, DateTimeOffset AlteradoEm);
