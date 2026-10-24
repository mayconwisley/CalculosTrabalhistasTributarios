namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <summary>Metadados do histórico; os dados do formulário são lidos somente ao abrir ou comparar.</summary>
public sealed record ResumoCalculoSalvoDto(long Id, string Tipo, string Calculadora, string Nome, DateTime CriadoEm, DateTime AlteradoEm);
