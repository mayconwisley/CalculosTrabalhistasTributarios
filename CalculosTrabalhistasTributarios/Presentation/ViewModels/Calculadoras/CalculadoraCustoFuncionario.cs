using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraCustoFuncionario : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularCustoFuncionarioRequest> _simulador;
    private readonly CampoTextoViewModel _salario = Moeda("Salário", "Salário mensal do empregado.");
    private readonly CampoOpcaoViewModel _regime = new("Regime da empresa",
        [
            new("Lucro Real ou Presumido", RegimeTributario.LucroRealOuPresumido),
            new("Simples (anexos I a III e V)", RegimeTributario.SimplesNacional),
            new("Simples (anexo IV)", RegimeTributario.SimplesNacionalAnexoIV),
            new("Empregador doméstico", RegimeTributario.EmpregadorDomestico)
        ], "Define a contribuição patronal sobre a folha; o empregador doméstico recolhe 8% + 0,8% + 3,2% no DAE.");
    private readonly CampoOpcaoViewModel _rat = new("RAT", [new("1% (risco leve)", 1m), new("2% (risco médio)", 2m), new("3% (risco grave)", 3m)], "Risco ambiental do trabalho, conforme a atividade da empresa.");
    private readonly CampoTextoViewModel _fap = new("FAP", TipoCampo.Numero, "1,0000", "Fator acidentário de prevenção, de 0,5 a 2, informado anualmente à empresa.");
    private readonly CampoTextoViewModel _terceiros = new("Terceiros (%)", TipoCampo.Numero, "5,8", "Sistema S, salário-educação e Incra; 5,8% é o mais comum.");
    private readonly CampoTextoViewModel _beneficios = Moeda("Benefícios", "Custo mensal com vale-transporte, alimentação e planos, já descontada a parte do empregado.");
    private readonly CampoOpcaoViewModel _provisoes = CampoOpcaoViewModel.SimNao("Incluir provisões", true, "Distribui o 13º e as férias com 1/3 mês a mês.");
    private readonly CampoOpcaoViewModel _aprendiz = CampoOpcaoViewModel.SimNao("Jovem aprendiz", false, "O aprendiz tem FGTS de 2% em vez de 8%.");

    public CalculadoraCustoFuncionario(ISimularDemonstrativoUseCase<SimularCustoFuncionarioRequest> simulador)
    {
        _simulador = simulador;
        _rat.Selecionada = _rat.Opcoes[1];
        _regime.AoAlterar = () =>
        {
            var regime = _regime.Valor<RegimeTributario>();
            _rat.Visivel = _fap.Visivel = regime is RegimeTributario.LucroRealOuPresumido or RegimeTributario.SimplesNacionalAnexoIV;
            _terceiros.Visivel = regime == RegimeTributario.LucroRealOuPresumido;
            _aprendiz.Visivel = regime != RegimeTributario.EmpregadorDomestico;
        };
        _regime.AoAlterar();
    }

    public override string Titulo => "Custo do funcionário";
    public override string Descricao => "Calcule quanto um empregado custa para a empresa, com encargos, provisões e benefícios.";
    public override string InstrucaoInicial => "Informe o salário, o regime da empresa e os benefícios e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_salario, _regime, _rat, _fap, _terceiros, _beneficios, _provisoes, _aprendiz];
    public override string NomeArquivoPdf => "custo-do-funcionario.pdf";

    protected override CampoTextoViewModel CampoSalario => _salario;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularCustoFuncionarioRequest(
            leitor.Moeda(_salario), _regime.Valor<RegimeTributario>(), _rat.Valor<decimal>(),
            _fap.Visivel ? leitor.Numero(_fap) : 1m, _terceiros.Visivel ? leitor.Numero(_terceiros) : 0m,
            leitor.Moeda(_beneficios), _provisoes.Valor<bool>(), _aprendiz.Visivel && _aprendiz.Valor<bool>()), cancellationToken);
}
