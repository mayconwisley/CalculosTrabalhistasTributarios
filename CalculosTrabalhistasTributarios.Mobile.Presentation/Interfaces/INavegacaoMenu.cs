using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;

/// <summary>Destinos do menu além das calculadoras.</summary>
public interface INavegacaoMenu
{
    Task AbrirTabelaAsync(TipoTabelaTributaria tabela);
    Task AbrirConfiguracoesAsync();
    Task AbrirHistoricoAsync();
    Task AbrirManualAsync();

    /// <summary>Abre o menu lateral, onde ficam as calculadoras e as tabelas.</summary>
    void AbrirMenu();

    /// <summary>Reabre um cálculo salvo na sua calculadora, já calculado.</summary>
    Task AbrirCalculoSalvoAsync(CalculoSalvoDto calculo);

    /// <summary>Volta à tela inicial, fechando as telas abertas por cima dela.</summary>
    Task VoltarAoInicioAsync();
}
