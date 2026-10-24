using System.Globalization;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Tributacao;

public sealed partial class SimulacaoTributariaViewModel(
    ISimularDemonstrativoUseCase<SimularImpostoRequest> simular,
    ICompartilhamentoPdf compartilhamento,
    ILogger<SimulacaoTributariaViewModel> logger) : ObservableObject, IFormularioHistoricoMobile
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    public string Titulo => "Simulação tributária";
    public long? HistoricoId { get; set; }
    public DadosFormularioMobile ExportarDados() => new(1, new Dictionary<string, ValorCampoMobile>
    {
        [nameof(Competencia)] = new(Competencia, 0, false), [nameof(ValorBruto)] = new(ValorBruto, 0, false),
        [nameof(BaseInss)] = new(BaseInss, 0, false), [nameof(Dependentes)] = new(Dependentes, 0, false),
        [nameof(UsarBrutoComoBase)] = new("", 0, UsarBrutoComoBase), [nameof(IndiceVinculo)] = new("", IndiceVinculo, false),
        [nameof(SessentaECincoAnos)] = new("", 0, SessentaECincoAnos), [nameof(DecimoTerceiro)] = new("", 0, DecimoTerceiro)
    }, new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, ValorCampoMobile>>>());

    public Result TentarImportarDados(DadosFormularioMobile dados)
    {
        var campos = dados.Campos;
        if (dados.Versao != 1 || campos is null || dados.Grades is null || dados.Grades.Count != 0
            || !campos.TryGetValue(nameof(Competencia), out var competencia) || !campos.TryGetValue(nameof(ValorBruto), out var bruto)
            || !campos.TryGetValue(nameof(BaseInss), out var baseInss) || !campos.TryGetValue(nameof(Dependentes), out var dependentes)
            || !campos.TryGetValue(nameof(UsarBrutoComoBase), out var usarBruto) || !campos.TryGetValue(nameof(IndiceVinculo), out var vinculo)
            || !campos.TryGetValue(nameof(SessentaECincoAnos), out var idade) || !campos.TryGetValue(nameof(DecimoTerceiro), out var decimo)
            || !new[] { competencia, bruto, baseInss, dependentes }.All(valor => valor is not null && valor.Valor is { Length: <= 40 })
            || vinculo is null || vinculo.Indice is < 0 or > 3 || usarBruto is null || idade is null || decimo is null)
            return Erro.Validacao("Formulário salvo: confira a competência, os valores e o tipo de vínculo.");
        Competencia = competencia.Valor; ValorBruto = bruto.Valor; BaseInss = baseInss.Valor; Dependentes = dependentes.Valor;
        UsarBrutoComoBase = usarBruto.Marcado; IndiceVinculo = vinculo.Indice;
        SessentaECincoAnos = idade.Marcado; DecimoTerceiro = decimo.Marcado;
        Demonstrativo = null;
        return Result.Ok();
    }
    public IReadOnlyList<string> Vinculos { get; } = ["Empregado", "Aprendiz", "Empregado doméstico", "Aposentadoria ou pensão"];
    [ObservableProperty] private string _competencia = DateTime.Today.ToString("MM/yyyy", Cultura);
    [ObservableProperty] private string _valorBruto = "0,00";
    [ObservableProperty] private string _baseInss = "0,00";
    [ObservableProperty] private string _dependentes = "0";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExibirBaseInss))]
    private bool _usarBrutoComoBase = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhAposentadoria))]
    [NotifyPropertyChangedFor(nameof(ExibirBaseInss))]
    private int _indiceVinculo;
    [ObservableProperty] private bool _sessentaECincoAnos;
    [ObservableProperty] private bool _decimoTerceiro;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemResultado))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    private DemonstrativoDto? _demonstrativo;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CalcularCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompartilharPdfCommand))]
    private bool _ocupado;

    public bool EhAposentadoria => IndiceVinculo == 3;
    public bool ExibirBaseInss => !EhAposentadoria && !UsarBrutoComoBase;
    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);
    public bool TemResultado => Demonstrativo is not null;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Competencia) or nameof(ValorBruto) or nameof(BaseInss) or nameof(Dependentes)
            or nameof(UsarBrutoComoBase) or nameof(IndiceVinculo) or nameof(SessentaECincoAnos) or nameof(DecimoTerceiro))
        {
            Demonstrativo = null;
            Mensagem = null;
        }
    }

    private bool PodeCalcular() => !Ocupado;
    private bool PodeCompartilhar() => !Ocupado && TemResultado;

    [RelayCommand(CanExecute = nameof(PodeCalcular))]
    private async Task CalcularAsync(CancellationToken cancellationToken)
    {
        Demonstrativo = null;
        Mensagem = null;
        if (!DateOnly.TryParseExact("01/" + Competencia.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var competencia))
        {
            Mensagem = "Competência: informe mês e ano no formato mm/aaaa, por exemplo 01/2026.";
            return;
        }
        if (!LeituraNumerica.TentarLer(ValorBruto, out var bruto))
        {
            Mensagem = "Valor bruto: informe um número, por exemplo 3.500,00.";
            return;
        }
        var baseInss = bruto;
        if (EhAposentadoria)
            baseInss = 0m;
        else if (!UsarBrutoComoBase && !LeituraNumerica.TentarLer(BaseInss, out baseInss))
        {
            Mensagem = "Base de INSS: informe um número, por exemplo 3.500,00.";
            return;
        }
        if (!int.TryParse(Dependentes, NumberStyles.Integer, Cultura, out var dependentes))
        {
            Mensagem = "Dependentes: informe uma quantidade inteira, por exemplo 0 ou 2.";
            return;
        }
        if (IndiceVinculo is < 0 or > 3)
        {
            Mensagem = "Tipo de vínculo: selecione uma opção da lista.";
            return;
        }
        var vinculo = IndiceVinculo switch
        {
            1 => VinculoSimulacao.Aprendiz,
            2 => VinculoSimulacao.Domestico,
            3 => VinculoSimulacao.Aposentadoria,
            _ => VinculoSimulacao.Empregado
        };
        var pedido = new SimularImpostoRequest(competencia, bruto, baseInss, dependentes, vinculo,
            EhAposentadoria && SessentaECincoAnos, EhAposentadoria && DecimoTerceiro);
        Ocupado = true;
        try
        {
            var resultado = await simular.ExecutarAsync(pedido, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (resultado.Falhou)
                Mensagem = resultado.Erro.Mensagem;
            else
                Demonstrativo = resultado.Valor;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha na simulação tributária móvel.");
            Mensagem = "Não foi possível preparar as tabelas ou calcular. Tente novamente.";
        }
        finally { Ocupado = false; }
    }

    [RelayCommand(CanExecute = nameof(PodeCompartilhar))]
    private async Task CompartilharPdfAsync(CancellationToken cancellationToken)
    {
        if (Demonstrativo is not { } demonstrativo)
            return;
        Ocupado = true;
        Mensagem = null;
        try { await compartilhamento.CompartilharAsync(demonstrativo, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao compartilhar PDF tributário.");
            Mensagem = "Não foi possível compartilhar o PDF. Confira o espaço disponível e tente novamente.";
        }
        finally { Ocupado = false; }
    }
}
