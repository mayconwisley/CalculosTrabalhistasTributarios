using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.Interfaces;

/// <summary>Comandos que a janela principal liga aos atalhos Ctrl+Enter, Ctrl+S, Ctrl+P e Ctrl+E da tela de cálculo aberta.</summary>
public interface IAtalhosDeCalculo
{
    ICommand CalcularCommand { get; }
    ICommand ExportarExcelCommand { get; }

    /// <summary>Nulo quando a tela não exporta PDF.</summary>
    ICommand? ExportarPdfCommand => null;

    HistoricoDaJanela Historico { get; }
}
