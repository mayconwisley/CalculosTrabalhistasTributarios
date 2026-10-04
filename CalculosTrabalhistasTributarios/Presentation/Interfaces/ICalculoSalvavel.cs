using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

/// <summary>Janela de cálculo que pode ser salva no histórico e reaberta com os mesmos valores.</summary>
public interface ICalculoSalvavel
{
    /// <summary>Janela do cálculo, gravada no histórico para reabri-lo na mesma janela.</summary>
    string TipoHistorico { get; }

    /// <summary>Nome da calculadora, exibido na lista do histórico.</summary>
    string NomeCalculadora { get; }

    /// <summary>Nome proposto na primeira vez que o cálculo é salvo.</summary>
    string NomeSugerido { get; }

    DadosFormulario ExportarDados();

    /// <summary>Preenche o formulário; valores que não existem mais na janela são ignorados.</summary>
    void ImportarDados(DadosFormulario dados);

    /// <summary>Refaz o cálculo com os valores carregados, para a janela abrir com o resultado.</summary>
    Task RecalcularAsync();
}
