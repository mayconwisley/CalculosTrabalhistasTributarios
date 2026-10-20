using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

public interface IAtualizacaoTabelasService
{
    /// <summary>Atualiza todas as tabelas pela internet mostrando o andamento; devolve o resultado de cada uma, ou vazio se interrompida.</summary>
    Task<IReadOnlyList<AtualizacaoDeTabela>> AtualizarAgoraAsync();
}
