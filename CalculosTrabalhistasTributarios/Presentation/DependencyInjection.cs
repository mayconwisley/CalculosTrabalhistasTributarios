using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Services;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using CalculosTrabalhistasTributarios.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CalculosTrabalhistasTributarios.Presentation;

public static class DependencyInjection
{
    /// <summary>Janelas, view models, serviços do WPF e as calculadoras da janela padrão.</summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services) => services
        .AddScoped<MainWindow>()
        .AddScoped<MainWindowViewModel>()
        .AddScoped<HistoricoViewModel>()
        .AddScoped<ITabelaManutencaoViewModelFactory, TabelaManutencaoViewModelFactory>()
        .AddScoped<ISimulacaoTributariaViewModelFactory, SimulacaoTributariaViewModelFactory>()
        .AddScoped<IPensaoViewModelFactory, PensaoViewModelFactory>()
        .AddScoped<IPensaoAtrasoViewModelFactory, PensaoAtrasoViewModelFactory>()
        // Uma só memória do último cálculo, compartilhada por todas as janelas.
        .AddSingleton<ContextoCompartilhado>()
        .AddScoped<IEstabilidadeViewModelFactory, EstabilidadeViewModelFactory>()
        .AddSingleton<IUserNotifier, WpfUserNotifier>()
        .AddSingleton<IExecutorInstalador, WpfExecutorInstalador>()
        .AddSingleton<IArquivoDialogService, WpfArquivoDialogService>()
        .AddSingleton<IManualUsuarioService, WpfManualUsuarioService>()
        .AddSingleton<INomeDialogService, WpfNomeDialogService>()
        .AddSingleton<IHistoricoDaJanelaFactory, HistoricoDaJanelaFactory>()
        .AddScoped<IWindowNavigator, WpfWindowNavigator>()
        .AddScoped<IDebitoJudicialViewModelFactory, DebitoJudicialViewModelFactory>()
        .AddScoped<IJornadaViewModelFactory, JornadaViewModelFactory>()
        .AddScoped<ICalculadoraViewModelFactory, CalculadoraViewModelFactory>()
        .AddCalculadoras();

    // Cada janela de calculadora recebe uma calculadora nova, com o formulário em branco.
    private static IServiceCollection AddCalculadoras(this IServiceCollection services) => services
        .AddKeyedTransient<ICalculadora, CalculadoraSalarioPeloLiquido>(TipoCalculadora.SalarioPeloLiquido)
        .AddKeyedTransient<ICalculadora, CalculadoraDecimoTerceiro>(TipoCalculadora.DecimoTerceiro)
        .AddKeyedTransient<ICalculadora, CalculadoraFerias>(TipoCalculadora.Ferias)
        .AddKeyedTransient<ICalculadora, CalculadoraHorasExtras>(TipoCalculadora.HorasExtras)
        .AddKeyedTransient<ICalculadora, CalculadoraComissoes>(TipoCalculadora.Comissoes)
        .AddKeyedTransient<ICalculadora, CalculadoraReajusteRetroativo>(TipoCalculadora.ReajusteRetroativo)
        .AddKeyedTransient<ICalculadora, CalculadoraMediaVerbasVariaveis>(TipoCalculadora.MediaVerbasVariaveis)
        .AddKeyedTransient<ICalculadora, CalculadoraBancoHoras>(TipoCalculadora.BancoHoras)
        .AddKeyedTransient<ICalculadora, CalculadoraInssMultiplosVinculos>(TipoCalculadora.InssMultiplosVinculos)
        .AddKeyedTransient<ICalculadora, CalculadoraRescisao>(TipoCalculadora.Rescisao)
        .AddKeyedTransient<ICalculadora, CalculadoraCustoFuncionario>(TipoCalculadora.CustoFuncionario)
        .AddKeyedTransient<ICalculadora, CalculadoraProLabore>(TipoCalculadora.ProLaboreAutonomo)
        .AddKeyedTransient<ICalculadora, CalculadoraPlr>(TipoCalculadora.Plr)
        .AddKeyedTransient<ICalculadora, CalculadoraSalarioFamilia>(TipoCalculadora.SalarioFamilia)
        .AddKeyedTransient<ICalculadora, CalculadoraAdicionais>(TipoCalculadora.Adicionais)
        .AddKeyedTransient<ICalculadora, CalculadoraRevisaoPensao>(TipoCalculadora.RevisaoPensao)
        .AddKeyedTransient<ICalculadora, CalculadoraSeguroDesemprego>(TipoCalculadora.SeguroDesemprego)
        .AddKeyedTransient<ICalculadora, CalculadoraCltPj>(TipoCalculadora.CltPj)
        .AddKeyedTransient<ICalculadora, CalculadoraHolerite>(TipoCalculadora.Holerite)
        .AddKeyedTransient<ICalculadora, CalculadoraIrpfAnual>(TipoCalculadora.IrpfAnual)
        .AddKeyedTransient<ICalculadora, CalculadoraDividendos>(TipoCalculadora.Dividendos)
        .AddKeyedTransient<ICalculadora, CalculadoraTributoAtraso>(TipoCalculadora.TributoAtraso)
        .AddKeyedTransient<ICalculadora, CalculadoraCorrecaoValor>(TipoCalculadora.CorrecaoValor)
        .AddKeyedTransient<ICalculadora, CalculadoraDomestico>(TipoCalculadora.Domestico)
        .AddKeyedTransient<ICalculadora, CalculadoraAfastamento>(TipoCalculadora.Afastamento)
        .AddKeyedTransient<ICalculadora, CalculadoraSaqueAniversario>(TipoCalculadora.SaqueAniversario)
        .AddKeyedTransient<ICalculadora, CalculadoraAbonoSalarial>(TipoCalculadora.AbonoSalarial)
        .AddKeyedTransient<ICalculadora, CalculadoraCarneLeao>(TipoCalculadora.CarneLeao)
        .AddKeyedTransient<ICalculadora, CalculadoraTrabalhoExterior>(TipoCalculadora.TrabalhoExterior)
        .AddKeyedTransient<ICalculadora, CalculadoraCreditoTrabalhador>(TipoCalculadora.CreditoTrabalhador)
        .AddKeyedTransient<ICalculadora, CalculadoraGanhoCapital>(TipoCalculadora.GanhoCapital)
        .AddKeyedTransient<ICalculadora, CalculadoraEstagio>(TipoCalculadora.Estagio)
        .AddKeyedTransient<ICalculadora, CalculadoraIntermitente>(TipoCalculadora.Intermitente);
}
