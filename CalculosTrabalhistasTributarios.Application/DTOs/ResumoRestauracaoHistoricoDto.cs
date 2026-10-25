namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Incluidos">Cálculos acrescentados ao histórico.</param>
/// <param name="JaExistentes">Cálculos da cópia que já estavam no histórico e não foram repetidos.</param>
public sealed record ResumoRestauracaoHistoricoDto(int Incluidos, int JaExistentes);
