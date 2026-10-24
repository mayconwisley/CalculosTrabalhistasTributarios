using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.Input;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class ReajusteRetroativoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private const int LimiteMeses = 120;
    private const int LimiteReflexos = 10;
    private const string AvosDecimoTerceiro = "12";
    private const string DiasFerias = "30";
    private const decimal LimiteMensal = 1_000_000_000m;

    private readonly ISimularDemonstrativoUseCase<SimularReajusteRetroativoRequest> _simulador;
    private readonly GradeMobile _meses;
    private readonly GradeMobile _reflexos;
    private readonly INavegacaoTransferenciasMobile? _transferencias;
    public override bool PodeCalcularRra => Demonstrativo?.ParcelasRra is { Count: > 0 } && _transferencias is not null;
    public override ICommand CalcularRraCommand { get; }

    public override string Titulo => "Diferenças de reajuste retroativo";
    public override string Descricao => "Compare o que foi pago com o salário reajustado em cada mês e apure os reflexos informados em 13º e férias.";

    public ReajusteRetroativoMobileViewModel(ISimularDemonstrativoUseCase<SimularReajusteRetroativoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<ReajusteRetroativoMobileViewModel> logger,
        INavegacaoTransferenciasMobile? transferencias = null) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        _transferencias = transferencias;
        CalcularRraCommand = new AsyncRelayCommand(CalcularRraAsync);
        Secao("periodo", "1. Gere as competências");
        Adicionar("inicio", "Início (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-3).ToString("MM/yyyy", Cultura),
            "Primeiro mês com direito ao reajuste.");
        Adicionar("fim", "Fim (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura),
            "Último mês pago sem o reajuste; até 120 meses no período.");
        Adicionar("salarioAnterior", "Salário anterior (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor pago antes do reajuste; preenche as linhas novas de Gerar meses.");
        Adicionar("percentual", "Reajuste (%)", TipoCampoMobile.Numero, "0,00",
            "Maior que zero e até 1.000%. O salário devido das linhas novas já inclui o reajuste.");

        _meses = AdicionarGrade("meses", "2. Revise o que foi pago em cada mês",
            "Edite os valores nos meses com antecipação, promoção, afastamento ou férias. Gerar novamente preserva os meses já digitados.",
            "Mês", LimiteMeses,
            [
                new("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia),
                new("pago", "Salário pago (R$)", TipoCampoMobile.Moeda, "0,00"),
                new("devido", "Salário devido (R$)", TipoCampoMobile.Moeda, "0,00", "Salário com o reajuste.")
            ]);
        _meses.GerarCommand = new RelayCommand(GerarMeses);
        _meses.RotuloGerar = "Gerar meses";

        _reflexos = AdicionarGrade("reflexos", "3. 13º ou férias já pagos (opcional)",
            "Informe a base salarial paga e a correta. Os avos do 13º ou os dias de férias e o terço são calculados aqui.",
            "Reflexo", LimiteReflexos,
            [
                new("tipo", "Lançamento", TipoCampoMobile.Opcao, Opcoes: ["13º salário", "Férias gozadas + 1/3"]),
                new("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia),
                new("pago", "Base paga (R$)", TipoCampoMobile.Moeda, "0,00"),
                new("devido", "Base correta (R$)", TipoCampoMobile.Moeda, "0,00"),
                new("quantidade", "Avos (13º) ou dias (férias)", TipoCampoMobile.Inteiro, AvosDecimoTerceiro,
                    "13º: de 1 a 12 avos. Férias: de 1 a 30 dias.")
            ], AjustarReflexo);
    }

    private async Task CalcularRraAsync()
    {
        if (_transferencias is null || Demonstrativo?.ParcelasRra is not { Count: > 0 } parcelas) return;
        var linhas = parcelas.Select(parcela => (IReadOnlyDictionary<string, ValorCampoMobile>)new Dictionary<string, ValorCampoMobile>
        {
            ["tipo"] = new("", (int)parcela.Tipo, false),
            ["competencia"] = new(parcela.Competencia.ToString("MM/yyyy", Cultura), 0, false),
            ["valor"] = new(parcela.Valor.ToString("N2", Cultura), 0, false)
        }).ToArray();
        await _transferencias.AbrirComDadosAsync(TipoCalculadoraMobile.Rra, new DadosFormularioMobile(1,
            new Dictionary<string, ValorCampoMobile>(), new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, ValorCampoMobile>>>
            { ["parcelas"] = linhas }));
    }

    private void GerarMeses()
    {
        try
        {
            var inicio = Competencia("inicio");
            var fim = Competencia("fim");
            if (inicio > fim)
                throw new EntradaInvalidaException("Fim: informe um mês igual ou posterior ao início.");
            var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
            if (quantidade > LimiteMeses)
                throw new EntradaInvalidaException($"Selecione no máximo {LimiteMeses} competências (10 anos).");
            var salario = Numero("salarioAnterior");
            var percentual = Numero("percentual");
            if (salario <= 0m || salario > LimiteMensal || decimal.Round(salario, 2) != salario || percentual <= 0m || percentual > 1000m)
                throw new EntradaInvalidaException("Informe um salário anterior positivo de até R$ 1 bilhão e um reajuste maior que zero e até 1.000%.");
            var devido = decimal.Round(salario * (1m + percentual / 100m), 2, MidpointRounding.AwayFromZero);
            if (devido > LimiteMensal)
                throw new EntradaInvalidaException("O salário reajustado excede o limite de R$ 1 bilhão por mês.");

            var existentes = _meses.Linhas.ToHashSet();
            var chaves = Enumerable.Range(0, quantidade).Select(indice => inicio.AddMonths(indice).ToString("MM/yyyy", Cultura)).ToArray();
            _meses.Gerar(chaves, "competencia");
            // A base cria as linhas novas só com a competência: os valores iniciais vêm do salário anterior e do reajuste.
            var aviso = _meses.Aviso;
            foreach (var linha in _meses.Linhas.Where(linha => !existentes.Contains(linha)))
            {
                linha.Campo("pago").Valor = salario.ToString("N2", Cultura);
                linha.Campo("devido").Valor = devido.ToString("N2", Cultura);
            }
            _meses.Aviso = aviso;
        }
        catch (EntradaInvalidaException erro) { _meses.Aviso = erro.Message; }
    }

    // Na criação, a competência sugerida é o fim do período; ao trocar o tipo, a quantidade padrão acompanha a unidade.
    private void AjustarReflexo(LinhaGradeMobile linha)
    {
        var competencia = linha.Campo("competencia");
        if (!_reflexos.Linhas.Contains(linha) && competencia.Valor.Trim().Length == 0)
            competencia.Valor = Texto("fim");
        var quantidade = linha.Campo("quantidade");
        var ferias = linha.Campo("tipo").Indice == 1;
        if (ferias && quantidade.Valor.Trim() == AvosDecimoTerceiro) quantidade.Valor = DiasFerias;
        else if (!ferias && quantidade.Valor.Trim() == DiasFerias) quantidade.Valor = AvosDecimoTerceiro;
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        if (_meses.Linhas.Count == 0)
            throw new EntradaInvalidaException("Gere as competências antes de calcular.");
        var parcelas = new List<ParcelaReajusteRetroativo>(_meses.Linhas.Count + _reflexos.Linhas.Count);
        parcelas.AddRange(_meses.Ler(linha =>
            new ParcelaReajusteRetroativo(LeituraCampoMobile.Competencia(linha.Campo("competencia")), TipoParcelaReajuste.Salario,
                Valor(linha.Campo("pago")), Valor(linha.Campo("devido")), 1)));
        parcelas.AddRange(_reflexos.Ler(linha =>
            new ParcelaReajusteRetroativo(LeituraCampoMobile.Competencia(linha.Campo("competencia")),
                LeituraCampoMobile.Opcao(linha.Campo("tipo")) == 0 ? TipoParcelaReajuste.DecimoTerceiro : TipoParcelaReajuste.FeriasGozadas,
                Valor(linha.Campo("pago")), Valor(linha.Campo("devido")), LeituraCampoMobile.Inteiro(linha.Campo("quantidade")))));
        return _simulador.ExecutarAsync(new SimularReajusteRetroativoRequest(parcelas), cancellationToken);
    }

    private static decimal Valor(CampoMobile campo)
    {
        var valor = LeituraCampoMobile.Numero(campo);
        if (valor < 0m || decimal.Round(valor, 2) != valor)
            throw new EntradaInvalidaException($"{campo.Rotulo}: informe um valor positivo ou zero, em reais e centavos.");
        return valor;
    }
}
