using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

/// <summary>Destinos do menu além das calculadoras.</summary>
public interface INavegacaoMenu
{
    Task AbrirTabelaAsync(TipoTabelaTributaria tabela);
    Task AbrirConfiguracoesAsync();

    /// <summary>Volta à tela inicial, fechando as telas abertas por cima dela.</summary>
    Task VoltarAoInicioAsync();
}
