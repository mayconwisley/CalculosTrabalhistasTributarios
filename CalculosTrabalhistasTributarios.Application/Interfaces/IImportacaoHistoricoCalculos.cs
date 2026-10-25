using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Inclusão em lote dos cálculos de uma cópia de segurança, numa única transação.</summary>
public interface IImportacaoHistoricoCalculos
{
    /// <summary>
    /// Inclui os cálculos com as datas originais, ignorando o <see cref="CalculoSalvoDto.Id"/>. Um cálculo igual a um já salvo
    /// (mesmo tipo, nome, formulário e data de criação) não é repetido, para que restaurar a mesma cópia duas vezes não duplique
    /// o histórico. Devolve quantos foram incluídos.
    /// </summary>
    Task<int> IncluirAsync(IReadOnlyList<CalculoSalvoDto> calculos, CancellationToken cancellationToken);
}
