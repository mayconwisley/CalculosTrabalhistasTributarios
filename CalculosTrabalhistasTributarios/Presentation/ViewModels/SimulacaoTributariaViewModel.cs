using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.Services;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using System.Globalization;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels;

/// <summary>Simulação do IRRF nas duas modalidades, do INSS por faixas, do salário líquido e do FGTS de uma competência.</summary>
public sealed class SimulacaoTributariaViewModel : ViewModelBase, ICalculoSalvavel
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly ISimularImpostoUseCase _simularImposto;
    private readonly IUserNotifier _notificador;
    private readonly IRelatorioPdfService _relatorioPdf;
    private readonly IPlanilhaService _planilha;
    private readonly IArquivoDialogService _arquivoDialog;
    private readonly ContextoCompartilhado _contexto;
    private string _competencia;
    private string _valorBruto = "0,00";
    private string _baseInss = "0,00";
    private string _dependentes;
    private SimulacaoImpostoDto? _ultimaSimulacao;
    private bool _temResultado;
    private IReadOnlyList<IndicadorResumoViewModel> _indicadoresResumo = [];
    private IReadOnlyList<ComparativoIrrfViewModel> _comparativoIrrf = [];
    private IReadOnlyList<SecaoMemoriaIrrfViewModel> _memoriaIrrf = [];
    private IReadOnlyList<SecaoMemoriaTributariaViewModel> _memoriaTributaria = [];

    public SimulacaoTributariaViewModel(ISimularImpostoUseCase simularImposto, IUserNotifier notificador, IRelatorioPdfService relatorioPdf, IPlanilhaService planilha, IArquivoDialogService arquivoDialog, ContextoCompartilhado contexto,
        IHistoricoDaJanelaFactory historico)
    {
        _simularImposto = simularImposto;
        _notificador = notificador;
        _relatorioPdf = relatorioPdf;
        _planilha = planilha;
        _arquivoDialog = arquivoDialog;
        _contexto = contexto;
        var anterior = contexto.Atual;
        _competencia = (anterior.Competencia ?? DateOnly.FromDateTime(DateTime.Today)).ToString("MM/yyyy", CulturaPtBr);
        _dependentes = (anterior.Dependentes ?? 0).ToString(CulturaPtBr);
        if (anterior.Salario is { } salario)
            ValorBruto = salario.ToString("N2", CulturaPtBr);
        CalcularCommand = new AsyncRelayCommand(CalcularAsync);
        Historico = historico.Criar(this);
        ExportarPdfCommand = new AsyncRelayCommand(ExportarPdfAsync, () => _ultimaSimulacao is not null);
        ExportarExcelCommand = new AsyncRelayCommand(
            () => ExportacaoPlanilha.SalvarAsync(_arquivoDialog, _notificador, $"simulacao-tributaria-{_ultimaSimulacao!.Entrada.Competencia:MM-yyyy}.xlsx",
                caminho => _planilha.GerarImpostoAsync(_ultimaSimulacao!, caminho, CancellationToken.None)),
            () => _ultimaSimulacao is not null);
    }

    public string Competencia { get => _competencia; set => SetProperty(ref _competencia, value); }

    /// <summary>Ao alterar o valor bruto, a base de INSS acompanha; ela só precisa ser editada quando for diferente.</summary>
    public string ValorBruto
    {
        get => _valorBruto;
        set
        {
            if (SetProperty(ref _valorBruto, value) && TentarLerDecimal(value, out var valorBruto) && valorBruto >= 0m)
                BaseInss = valorBruto.ToString("N2", CulturaPtBr);
        }
    }
    public string BaseInss { get => _baseInss; set => SetProperty(ref _baseInss, value); }
    public string Dependentes { get => _dependentes; set => SetProperty(ref _dependentes, value); }
    public bool TemResultado { get => _temResultado; private set => SetProperty(ref _temResultado, value); }
    public IReadOnlyList<IndicadorResumoViewModel> IndicadoresResumo { get => _indicadoresResumo; private set => SetProperty(ref _indicadoresResumo, value); }
    public IReadOnlyList<ComparativoIrrfViewModel> ComparativoIrrf { get => _comparativoIrrf; private set => SetProperty(ref _comparativoIrrf, value); }
    public IReadOnlyList<SecaoMemoriaIrrfViewModel> MemoriaIrrf { get => _memoriaIrrf; private set => SetProperty(ref _memoriaIrrf, value); }
    public IReadOnlyList<SecaoMemoriaTributariaViewModel> MemoriaTributaria { get => _memoriaTributaria; private set => SetProperty(ref _memoriaTributaria, value); }

    public ICommand CalcularCommand { get; }
    public ICommand ExportarPdfCommand { get; }
    public ICommand ExportarExcelCommand { get; }
    public HistoricoDaJanela Historico { get; }

    public string TipoHistorico => "SimulacaoTributaria";
    public string NomeCalculadora => "Simulação tributária";
    public string NomeSugerido => $"Simulação tributária - {Competencia}";

    public DadosFormulario ExportarDados() => new(new()
    {
        [nameof(Competencia)] = Competencia,
        [nameof(ValorBruto)] = ValorBruto,
        [nameof(BaseInss)] = BaseInss,
        [nameof(Dependentes)] = Dependentes
    });

    public void ImportarDados(DadosFormulario dados)
    {
        Competencia = dados.Valor(nameof(Competencia), Competencia);
        // O valor bruto preenche a base de INSS; a base salva vem depois, para valer a que foi informada.
        ValorBruto = dados.Valor(nameof(ValorBruto), ValorBruto);
        BaseInss = dados.Valor(nameof(BaseInss), BaseInss);
        Dependentes = dados.Valor(nameof(Dependentes), Dependentes);
    }

    public Task RecalcularAsync() => CalcularAsync();

    private async Task CalcularAsync()
    {
        if (!TentarLerEntrada(out var entrada))
            return;

        try
        {
            var simulacao = await _simularImposto.ExecutarAsync(entrada, CancellationToken.None);
            if (simulacao.Falhou)
            {
                _notificador.MostrarFalha(simulacao.Erro, "Não foi possível concluir o cálculo.");
                return;
            }
            var resultado = simulacao.Valor;
            _ultimaSimulacao = resultado;
            _contexto.Registrar(new ContextoCalculo(entrada.Competencia, entrada.ValorBruto > 0m ? entrada.ValorBruto : null, entrada.QuantidadeDependentes));
            AtualizarApresentacao(resultado);
            ((AsyncRelayCommand)ExportarPdfCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ExportarExcelCommand).RaiseCanExecuteChanged();
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível concluir o cálculo.", exception);
        }
    }

    private async Task ExportarPdfAsync()
    {
        if (_ultimaSimulacao is null)
            return;

        var caminhoArquivo = _arquivoDialog.SolicitarDestinoPdf($"relatorio-simulacao-tributaria-{_ultimaSimulacao.Entrada.Competencia:MM-yyyy}.pdf");
        if (caminhoArquivo is null)
            return;

        try
        {
            await _relatorioPdf.GerarRelatorioImpostoAsync(_ultimaSimulacao, caminhoArquivo, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _notificador.MostrarErro("Não foi possível gerar o relatório em PDF.", exception);
        }
    }

    private bool TentarLerEntrada(out SimularImpostoRequest entrada)
    {
        entrada = default!;
        if (!TentarLerCompetencia(out var competencia) || !TentarLerDecimal(ValorBruto, out var valorBruto) || !TentarLerDecimal(BaseInss, out var baseInss) || !int.TryParse(Dependentes, out var dependentes) || valorBruto < 0m || baseInss < 0m || dependentes < 0)
        {
            _notificador.MostrarAviso("Informe uma competência válida (MM/AAAA), valores monetários válidos e dependentes maior ou igual a zero.");
            return false;
        }

        entrada = new SimularImpostoRequest(competencia, valorBruto, baseInss, dependentes);
        return true;
    }

    private bool TentarLerCompetencia(out DateOnly competencia)
    {
        competencia = default;
        if (!DateTime.TryParseExact(Competencia.Trim(), "MM/yyyy", CulturaPtBr, DateTimeStyles.None, out var data))
            return false;

        competencia = DateOnly.FromDateTime(data);
        return true;
    }

    private static bool TentarLerDecimal(string valor, out decimal resultado) => LeituraNumerica.TentarLer(valor, out resultado);

    private void AtualizarApresentacao(SimulacaoImpostoDto simulacao)
    {
        IndicadoresResumo =
        [
            new("Valor bruto", Moeda(simulacao.Entrada.ValorBruto), $"Competência {simulacao.Entrada.Competencia:MM/yyyy}"),
            new("INSS", Moeda(simulacao.ValorInss), $"Base: {Moeda(simulacao.BaseInssConsiderada)}"),
            new("IRRF normal", Moeda(simulacao.Normal.Imposto), $"Alíquota efetiva: {Percentual(simulacao.Normal.AliquotaEfetiva)}"),
            simulacao.DescontoSimplificado is null
                ? new("IRRF simplificado", "Não se aplica", "Existe a partir de 05/2023")
                : new("IRRF simplificado", Moeda(simulacao.Simplificada.Imposto), $"Alíquota efetiva: {Percentual(simulacao.Simplificada.AliquotaEfetiva)}"),
            new("Salário líquido", Moeda(simulacao.SalarioLiquido), simulacao.RetencaoDispensada ? $"IRRF de {Moeda(simulacao.IrrfCalculado)} dispensado" : "Bruto menos INSS e o menor IRRF"),
            new("FGTS padrão", Moeda(simulacao.FgtsOitoPorCento), "Alíquota de 8%"),
            new("FGTS Jovem Aprendiz", Moeda(simulacao.FgtsDoisPorCento), "Alíquota de 2%")
        ];

        // Antes de 05/2023 não havia desconto simplificado: só a modalidade normal aparece.
        var temSimplificado = simulacao.DescontoSimplificado is not null;
        ComparativoIrrf = temSimplificado
            ? [CriarComparativo(simulacao.Normal, simulacao.ModalidadeMaisVantajosa), CriarComparativo(simulacao.Simplificada, simulacao.ModalidadeMaisVantajosa)]
            : [CriarComparativo(simulacao.Normal, simulacao.ModalidadeMaisVantajosa)];

        var memoriaIrrf = new List<SecaoMemoriaIrrfViewModel> { CriarMemoriaIrrf(simulacao.Normal, FormulaBaseIrrf.Normal(simulacao, Moeda)) };
        if (temSimplificado)
            memoriaIrrf.Add(CriarMemoriaIrrf(simulacao.Simplificada, FormulaBaseIrrf.Simplificada(simulacao, Moeda)));
        memoriaIrrf.Add(new("Salário líquido", $"Líquido: {Moeda(simulacao.SalarioLiquido)}",
        [
            new("Valor bruto - INSS - IRRF", $"{Moeda(simulacao.Entrada.ValorBruto)} - {Moeda(simulacao.ValorInss)} - {Moeda(simulacao.IrrfAplicado)} = {Moeda(simulacao.SalarioLiquido)}"),
            new("IRRF descontado", DescreverIrrfDescontado(simulacao))
        ]));
        MemoriaIrrf = memoriaIrrf;

        var memoriaTributaria = new List<SecaoMemoriaTributariaViewModel>
        {
            CriarSecaoMemoria("INSS progressivo", simulacao.DetalhesInss, simulacao.ValorInss),
            CriarSecaoMemoria(TituloProgressivo("IRRF normal", simulacao.Normal), simulacao.Normal.DetalhesProgressivos, simulacao.Normal.ImpostoAntesReducao)
        };
        if (temSimplificado)
            memoriaTributaria.Add(CriarSecaoMemoria(TituloProgressivo("IRRF simplificado", simulacao.Simplificada), simulacao.Simplificada.DetalhesProgressivos, simulacao.Simplificada.ImpostoAntesReducao));
        MemoriaTributaria = memoriaTributaria;
        TemResultado = true;
    }

    private static ComparativoIrrfViewModel CriarComparativo(ModalidadeIrrfDto modalidade, string? modalidadeMaisVantajosa) =>
        new(
            modalidade.Nome,
            Moeda(modalidade.BaseCalculo),
            Moeda(modalidade.ReducaoMensal),
            Moeda(modalidade.Imposto),
            modalidade.Nome.Equals(modalidadeMaisVantajosa, StringComparison.OrdinalIgnoreCase));

    private static string DescreverIrrfDescontado(SimulacaoImpostoDto simulacao)
    {
        if (simulacao.RetencaoDispensada)
            return $"O IRRF calculado de {Moeda(simulacao.IrrfCalculado)} não passa de {Moeda(simulacao.DescontoMinimo)} e não é retido (Lei 9.430/1996, art. 67).";
        if (simulacao.DescontoSimplificado is null)
            return "O da modalidade normal (deduções legais): o desconto simplificado só existe a partir de 05/2023.";
        return simulacao.SimplificadaAplicada
            ? "O da modalidade simplificada, que resulta em imposto menor."
            : "O da modalidade normal (deduções legais), que resulta em imposto menor ou igual ao simplificado.";
    }

    // As faixas são arredondadas uma a uma; o total é o imposto pela parcela a deduzir, antes da redução mensal.
    private static string TituloProgressivo(string nome, ModalidadeIrrfDto modalidade) =>
        modalidade.ReducaoMensal > 0m ? $"{nome} - cálculo progressivo, antes da redução mensal" : $"{nome} - cálculo progressivo";

    private static SecaoMemoriaTributariaViewModel CriarSecaoMemoria(string titulo, IReadOnlyList<DetalheFaixaDto> detalhes, decimal total) =>
        new(titulo, Moeda(total), detalhes.Select(detalhe => new LinhaFaixaTributariaViewModel($"Faixa {detalhe.Faixa}", Moeda(detalhe.BaseCalculada), Percentual(detalhe.Aliquota), Moeda(detalhe.Imposto))).ToArray());

    private static SecaoMemoriaIrrfViewModel CriarMemoriaIrrf(ModalidadeIrrfDto modalidade, string formulaBase) =>
        new($"IRRF {modalidade.Nome}", $"IRRF final: {Moeda(modalidade.Imposto)}",
        [
            new("Base de cálculo", formulaBase),
            new("IR progressivo", $"{Moeda(modalidade.BaseCalculo)} x {Percentual(modalidade.Aliquota)} - {Moeda(modalidade.Deducao)} = {Moeda(modalidade.ImpostoAntesReducao)}"),
            new("Redução mensal", $"{Moeda(modalidade.ImpostoAntesReducao)} - {Moeda(modalidade.ReducaoMensal)} = {Moeda(modalidade.Imposto)}")
        ]);

    private static string Moeda(decimal valor) => valor.ToString("C2", CulturaPtBr);
    private static string Percentual(decimal valor) => valor.ToString("N2", CulturaPtBr) + "%";
}
