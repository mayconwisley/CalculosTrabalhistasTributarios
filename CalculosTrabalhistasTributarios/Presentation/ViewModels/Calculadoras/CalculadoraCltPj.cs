using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraCltPj(ISimularDemonstrativoUseCase<SimularCltPjRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia();
    private readonly CampoTextoViewModel _salario = Moeda("Salário CLT", "Salário mensal como empregado.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução do IRRF, nos dois regimes.");
    private readonly CampoTextoViewModel _beneficios = Moeda("Benefícios do CLT", "Vale-refeição, vale-alimentação, plano de saúde e outros benefícios pagos pela empresa por mês.");
    private readonly CampoOpcaoViewModel _regime = new("Regime da empresa",
        [
            new("Lucro Real ou Presumido", RegimeTributario.LucroRealOuPresumido),
            new("Simples (anexos I a III e V)", RegimeTributario.SimplesNacional),
            new("Simples (anexo IV)", RegimeTributario.SimplesNacionalAnexoIV)
        ], "Regime da empresa contratante, que define os encargos sobre o salário do CLT.");
    private readonly CampoTextoViewModel _valorPj = Moeda("Valor mensal como PJ", "Valor da nota fiscal por mês. Deixe 0,00 para calcular o valor que iguala o total do CLT.");
    private readonly CampoOpcaoViewModel _tributacao = new("Simples Nacional do PJ",
        [
            new("Anexo III (fator R)", TributacaoPj.AnexoIIIFatorR),
            new("Anexo III", TributacaoPj.AnexoIII),
            new("Anexo V", TributacaoPj.AnexoV)
        ], "Fator R: com pró-labore de 28% do faturamento, serviços do anexo V passam para o III. Anexo III: atividade já tributada nele, com pró-labore de um salário mínimo. Anexo V: pró-labore de um salário mínimo.");
    private readonly CampoTextoViewModel _custosPj = Moeda("Custos mensais do PJ", "Contabilidade, taxas e outros custos fixos da empresa do PJ.");

    public override string Titulo => "CLT x PJ";
    public override string Descricao => "Compare o que sobra para o profissional e o custo para a empresa como CLT e como PJ no Simples Nacional.";
    public override string InstrucaoInicial => "Informe o salário CLT, os benefícios e o valor mensal como PJ, ou deixe 0,00 para calcular o equivalente, e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _salario, _dependentes, _beneficios, _regime, _valorPj, _tributacao, _custosPj];
    public override string NomeArquivoPdf => $"clt-x-pj-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularCltPjRequest(leitor.Competencia(_competencia), leitor.Moeda(_salario), leitor.Inteiro(_dependentes), leitor.Moeda(_beneficios),
            _regime.Valor<RegimeTributario>(), leitor.Moeda(_valorPj), _tributacao.Valor<TributacaoPj>(), leitor.Moeda(_custosPj)), cancellationToken);
}
