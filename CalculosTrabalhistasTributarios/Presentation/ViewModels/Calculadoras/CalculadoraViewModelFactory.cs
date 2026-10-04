using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>
/// Cada calculadora é registrada na injeção de dependências com o seu <see cref="TipoCalculadora"/> como chave e abre
/// preenchida com os dados do último cálculo.
/// </summary>
public sealed class CalculadoraViewModelFactory(IServiceProvider servicos, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha, IArquivoDialogService arquivoDialog,
    ContextoCompartilhado contexto, IHistoricoDaJanelaFactory historico) : ICalculadoraViewModelFactory
{
    public CalculadoraViewModel Criar(TipoCalculadora tipo)
    {
        var calculadora = servicos.GetRequiredKeyedService<ICalculadora>(tipo);
        calculadora.Preencher(contexto.Atual);
        return new CalculadoraViewModel(tipo, calculadora, notificador, relatorioPdf, planilha, arquivoDialog, contexto, historico);
    }
}
