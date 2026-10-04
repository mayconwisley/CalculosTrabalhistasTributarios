using CalculosTrabalhistasTributarios.Application.DTOs;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;

public sealed class ItemHistoricoViewModel(CalculoSalvoDto calculo)
{
    public CalculoSalvoDto Calculo { get; } = calculo;
    public string Nome => Calculo.Nome;
    public string Detalhe => $"{Calculo.Calculadora} • alterado em {Calculo.AlteradoEm:dd/MM/yyyy} às {Calculo.AlteradoEm:HH:mm}";
}
