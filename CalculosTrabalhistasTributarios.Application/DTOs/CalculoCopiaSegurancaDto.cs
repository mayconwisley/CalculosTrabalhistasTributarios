namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Cálculo salvo dentro da cópia de segurança, sem o identificador local do banco.</summary>
public sealed record CalculoCopiaSegurancaDto(string Tipo, string Calculadora, string Nome, string Dados, DateTime CriadoEm, DateTime AlteradoEm);
