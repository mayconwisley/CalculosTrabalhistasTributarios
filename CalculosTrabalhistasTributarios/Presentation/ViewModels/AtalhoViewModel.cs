using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Cartão da tela inicial que abre uma calculadora ou uma tabela.</summary>
public sealed record AtalhoViewModel(string Titulo, string Descricao, ICommand Abrir);
