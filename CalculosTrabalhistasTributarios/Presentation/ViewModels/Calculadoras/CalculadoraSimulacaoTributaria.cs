using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraSimulacaoTributaria : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularImpostoRequest> _simulador;
    private readonly CampoTextoViewModel _competencia = Competencia();
    private readonly CampoTextoViewModel _bruto = Moeda("Valor bruto", "Rendimento tributável do mês, base do IRRF.");
    private readonly CampoTextoViewModel _baseInss = Moeda("Base de INSS", "Acompanha o valor bruto; altere só quando parte do valor não tiver incidência de INSS. Também é a base do FGTS, sem teto.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução do IRRF nas deduções legais.");
    private readonly CampoOpcaoViewModel _vinculo = new("Tipo de vínculo",
        [new("Empregado (FGTS 8%)", CategoriaFgts.Empregado), new("Aprendiz (FGTS 2%)", CategoriaFgts.Aprendiz), new("Doméstico (FGTS 8% + 3,2%)", CategoriaFgts.Domestico)],
        "Define o FGTS: 8% em geral, 2% no contrato de aprendizagem; no doméstico, 8% e mais 3,2% de indenização compensatória.") { Largura = 250 };

    public CalculadoraSimulacaoTributaria(ISimularDemonstrativoUseCase<SimularImpostoRequest> simulador)
    {
        _simulador = simulador;
        // A base de INSS acompanha o valor bruto e só precisa ser editada quando for diferente. Na reabertura do histórico, o
        // valor bruto é importado antes da base, e a base salva prevalece.
        _bruto.PropertyChanged += (_, argumentos) =>
        {
            if (argumentos.PropertyName == nameof(CampoTextoViewModel.Valor) && LeituraNumerica.TentarLer(_bruto.Valor, out var bruto) && bruto >= 0m)
                _baseInss.Valor = bruto.ToString("N2", Cultura);
        };
    }

    public override string Titulo => "Simulação tributária";
    public override string Descricao => "IRRF nas duas modalidades, INSS por faixas, salário líquido e FGTS do tipo de vínculo.";
    public override string InstrucaoInicial => "Informe a competência, o valor bruto, os dependentes e o tipo de vínculo e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _bruto, _baseInss, _dependentes, _vinculo];
    public override string NomeArquivoPdf => $"simulacao-tributaria-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _bruto;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    // A janela própria, anterior à janela padrão, salvava os campos pelo nome da propriedade.
    protected override IReadOnlyDictionary<string, string> RotulosAnteriores { get; } = new Dictionary<string, string>
    {
        ["Competencia"] = "Competência",
        ["ValorBruto"] = "Valor bruto",
        ["BaseInss"] = "Base de INSS"
    };

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularImpostoRequest(leitor.Competencia(_competencia), leitor.Moeda(_bruto), leitor.Moeda(_baseInss), leitor.Inteiro(_dependentes),
            _vinculo.Valor<CategoriaFgts>()), cancellationToken);
}
