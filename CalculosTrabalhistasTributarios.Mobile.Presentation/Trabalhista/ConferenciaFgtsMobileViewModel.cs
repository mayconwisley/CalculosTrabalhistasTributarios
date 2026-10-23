using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.Input;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class ConferenciaFgtsMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularConferenciaFgtsRequest> _simulador;
    private readonly INavegacaoTransferenciasMobile? _transferencias;
    public override bool PodeLevarFgtsRescisao => Demonstrativo?.DepositosFgts is { Depositos.Count: > 0 } && _transferencias is not null;
    public override ICommand LevarFgtsRescisaoCommand { get; }

    public override string Titulo => "Conferência do FGTS";
    public override string Descricao => "Compare o FGTS devido em cada competência com o depositado e separe as diferenças mensais e rescisórias.";

    public ConferenciaFgtsMobileViewModel(ISimularDemonstrativoUseCase<SimularConferenciaFgtsRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<ConferenciaFgtsMobileViewModel> logger,
        INavegacaoTransferenciasMobile? transferencias = null) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        _transferencias = transferencias;
        LevarFgtsRescisaoCommand = new AsyncRelayCommand(LevarFgtsRescisaoAsync);
        // Mesma ordem de CategoriaFgts.
        Adicionar("categoria", "Categoria", TipoCampoMobile.Opcao,
            dica: "Define a alíquota: 8% em geral e 2% no aprendiz; no doméstico, 8% de FGTS e 3,2% de indenização compensatória.",
            opcoes: ["Empregado (8%)", "Aprendiz (2%)", "Doméstico (8% + 3,2%)"]);
        Adicionar("desligamento", "Desligamento (dd/MM/yyyy, opcional)", TipoCampoMobile.Data,
            dica: "Exigido para a competência rescisória. Deixe vazio sem rescisão.");
        Secao("gerarSecao", "Gerar competências");
        Adicionar("inicio", "Início (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-12).ToString("MM/yyyy", Cultura));
        Adicionar("fim", "Fim (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.AddMonths(-1).ToString("MM/yyyy", Cultura));
        Adicionar("remuneracaoMensal", "Remuneração por mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor sugerido nas competências novas; ajuste cada mês na grade.");
        Adicionar("depositoMensal", "Depósito por mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Depósito do extrato ou da guia sugerido nas competências novas.");
        // Mesma ordem de TipoCompetenciaFgts.
        var grade = AdicionarGrade("competencias", "Competências do FGTS",
            "Remuneração e depósito informado de cada competência. Gerar meses preserva os valores já digitados.",
            "Competência", ConferenciaFgts.LimiteLinhas,
            [
                new("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia),
                new("tipo", "Tipo", TipoCampoMobile.Opcao,
                    Dica: "Rescisória: competência do desligamento, com as verbas rescisórias que integram o FGTS, inclusive o aviso prévio indenizado.",
                    Opcoes: ["Mensal", "Rescisória"]),
                new("remuneracao", "Remuneração (R$)", TipoCampoMobile.Moeda, "0,00", "Base do FGTS da competência."),
                new("deposito", "Depósito informado (R$)", TipoCampoMobile.Moeda, "0,00", "Valor que consta no extrato ou na guia.")
            ]);
        grade.GerarCommand = new RelayCommand(GerarMeses);
    }

    private async Task LevarFgtsRescisaoAsync()
    {
        if (_transferencias is null || Demonstrativo?.DepositosFgts is not { Depositos.Count: > 0 } fgts) return;
        var campos = new Dictionary<string, ValorCampoMobile>();
        if (fgts.Desligamento is { } desligamento)
            campos["desligamento"] = new(desligamento.ToString("dd/MM/yyyy", Cultura), 0, false);
        await _transferencias.AbrirComDadosAsync(TipoCalculadoraMobile.Rescisao, new DadosFormularioMobile(1, campos,
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, ValorCampoMobile>>>(),
            DepositosFgts: fgts.Depositos.Select(item => new DepositoFgtsSalvoMobile(item.Competencia.ToString("MM/yyyy", Cultura),
                item.Valor.ToString("N2", Cultura))).ToArray()));
    }

    private void GerarMeses()
    {
        var grade = Grade("competencias");
        try
        {
            var inicio = Competencia("inicio");
            var fim = Competencia("fim");
            var remuneracao = Numero("remuneracaoMensal");
            var deposito = Numero("depositoMensal");
            if (inicio > fim)
                throw new EntradaInvalidaException("Fim (MM/yyyy): informe uma competência igual ou posterior ao início.");
            if (remuneracao < 0m || deposito < 0m)
                throw new EntradaInvalidaException("Informe remuneração e depósito por mês positivos ou zero, por exemplo 3.000,00.");
            var quantidade = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month + 1;
            if (quantidade > grade.Limite)
                throw new EntradaInvalidaException($"O período tem {quantidade} competências; a conferência aceita até {grade.Limite}.");
            var anteriores = grade.Linhas.ToHashSet();
            grade.Gerar([.. Enumerable.Range(0, quantidade).Select(indice => inicio.AddMonths(indice).ToString("MM/yyyy", Cultura))], "competencia");
            var aviso = grade.Aviso;
            foreach (var linha in grade.Linhas.Where(linha => !anteriores.Contains(linha)))
            {
                linha.Campo("remuneracao").Valor = remuneracao.ToString("N2", Cultura);
                linha.Campo("deposito").Valor = deposito.ToString("N2", Cultura);
            }
            grade.Aviso = aviso;
        }
        catch (EntradaInvalidaException erro) { grade.Aviso = erro.Message; }
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var grade = Grade("competencias");
        if (grade.Linhas.Count == 0)
            throw new EntradaInvalidaException("Gere os meses ou adicione as competências a conferir.");
        var lancamentos = grade.Ler(linha => new LancamentoFgts(
            LeituraCampoMobile.Competencia(linha.Campo("competencia")),
            (TipoCompetenciaFgts)LeituraCampoMobile.Opcao(linha.Campo("tipo")),
            LeituraCampoMobile.Numero(linha.Campo("remuneracao")),
            LeituraCampoMobile.Numero(linha.Campo("deposito"))));
        return _simulador.ExecutarAsync(new SimularConferenciaFgtsRequest((CategoriaFgts)Opcao("categoria"), lancamentos,
            DataOpcional("desligamento")), cancellationToken);
    }
}
