using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.Interfaces;

/// <summary>Atualização pela internet de cada tabela, usada pela janela de manutenção.</summary>
public interface IAtualizadorTabelas
{
    /// <summary>Página oficial consultada na atualização da tabela.</summary>
    Uri FonteOficial(TipoTabelaTributaria tipo);

    /// <summary>Órgão responsável pela página oficial, como "INSS", "Receita Federal" ou "Ministério do Trabalho".</summary>
    string NomeFonteOficial(TipoTabelaTributaria tipo);

    Task<Result<AtualizacaoTabelaResultado>> AtualizarAsync(TipoTabelaTributaria tipo, CancellationToken cancellationToken);
}
