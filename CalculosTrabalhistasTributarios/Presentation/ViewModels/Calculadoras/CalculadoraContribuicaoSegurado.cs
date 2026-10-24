using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraContribuicaoSegurado : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularContribuicaoSeguradoRequest> _simulador;
    private readonly CampoTextoViewModel _competencia = Competencia(dica: "Mês da contribuição (mm/aaaa): define o salário mínimo e o teto. No trimestral, qualquer mês do trimestre civil.");
    private readonly CampoOpcaoViewModel _categoria = new("Categoria",
        [new("Contribuinte individual (autônomo)", CategoriaSegurado.ContribuinteIndividual), new("Facultativo", CategoriaSegurado.Facultativo)],
        "Contribuinte individual: quem trabalha por conta própria. Facultativo: quem não tem atividade remunerada, como estudante ou dona de casa.") { Largura = 290 };
    private readonly CampoOpcaoViewModel _plano = new("Plano",
        [new("Normal (20%)", PlanoContribuicao.Normal), new("Simplificado (11% do mínimo)", PlanoContribuicao.Simplificado), new("Baixa renda (5% do mínimo)", PlanoContribuicao.BaixaRenda)],
        "Normal: 20% de uma base entre o mínimo e o teto. Simplificado: 11% do salário mínimo. Baixa renda: 5% do mínimo, só para o facultativo inscrito no CadÚnico.") { Largura = 290 };
    private readonly CampoTextoViewModel _salario = Moeda("Salário de contribuição",
        "Base do plano normal, entre o salário mínimo e o teto do INSS; acima do teto, a base é limitada a ele.");
    private readonly CampoOpcaoViewModel _periodicidade = new("Recolhimento",
        [new("Mensal", PeriodicidadeContribuicao.Mensal), new("Trimestral", PeriodicidadeContribuicao.Trimestral)],
        "O trimestral, com vencimento no dia 15 do mês seguinte ao trimestre, só vale para quem contribui sobre o salário mínimo.");

    public CalculadoraContribuicaoSegurado(ISimularDemonstrativoUseCase<SimularContribuicaoSeguradoRequest> simulador)
    {
        _simulador = simulador;
        _plano.AoAlterar = () => _salario.Visivel = _plano.Valor<PlanoContribuicao>() == PlanoContribuicao.Normal;
        _plano.AoAlterar();
    }

    public override string Titulo => "Contribuição do autônomo e do facultativo";
    public override string Descricao => "Calcule a GPS do contribuinte individual e do facultativo: valor, código, vencimento, complementação e os planos de 20%, 11% e 5% lado a lado.";
    public override string InstrucaoInicial => "Informe a competência, a categoria e o plano e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _categoria, _plano, _salario, _periodicidade];
    public override string NomeArquivoPdf => $"contribuicao-inss-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var plano = _plano.Valor<PlanoContribuicao>();
        return LerECalcularAsync(_simulador, leitor => new SimularContribuicaoSeguradoRequest(
            leitor.Competencia(_competencia), _categoria.Valor<CategoriaSegurado>(), plano, _periodicidade.Valor<PeriodicidadeContribuicao>(),
            plano == PlanoContribuicao.Normal ? leitor.Moeda(_salario) : 0m), cancellationToken);
    }
}
