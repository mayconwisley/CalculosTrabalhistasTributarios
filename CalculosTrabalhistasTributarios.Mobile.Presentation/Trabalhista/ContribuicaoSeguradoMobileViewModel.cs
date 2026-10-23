using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class ContribuicaoSeguradoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularContribuicaoSeguradoRequest> _simulador;

    public override string Titulo => "Contribuição do autônomo e do facultativo";
    public override string Descricao => "Calcule a GPS do contribuinte individual e do facultativo: valor, código, vencimento e os planos de 20%, 11% e 5%.";

    public ContribuicaoSeguradoMobileViewModel(ISimularDemonstrativoUseCase<SimularContribuicaoSeguradoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<ContribuicaoSeguradoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Define o salário mínimo e o teto. No trimestral, qualquer mês do trimestre civil.");
        // Opções na mesma ordem de CategoriaSegurado, PlanoContribuicao e PeriodicidadeContribuicao.
        Adicionar("categoria", "Categoria", TipoCampoMobile.Opcao,
            dica: "Contribuinte individual trabalha por conta própria; facultativo não tem atividade remunerada, como estudante ou dona de casa.",
            opcoes: ["Contribuinte individual (autônomo)", "Facultativo"]);
        Adicionar("plano", "Plano", TipoCampoMobile.Opcao,
            dica: "Normal: 20% de uma base entre o mínimo e o teto. Simplificado: 11% do mínimo. Baixa renda: 5% do mínimo, só para o facultativo inscrito no CadÚnico.",
            opcoes: ["Normal (20%)", "Simplificado (11% do mínimo)", "Baixa renda (5% do mínimo)"]);
        Adicionar("salario", "Salário de contribuição (R$)", TipoCampoMobile.Moeda, "0,00",
            "Entre o salário mínimo e o teto do INSS; acima do teto, a base é limitada a ele.");
        Adicionar("periodicidade", "Recolhimento", TipoCampoMobile.Opcao,
            dica: "O trimestral, com vencimento no dia 15 do mês seguinte ao trimestre, só vale para quem contribui sobre o salário mínimo.",
            opcoes: ["Mensal", "Trimestral"]);
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "plano") Atualizar();
    }

    private void Atualizar() => Campo("salario").Visivel = Campo("plano").Indice == (int)PlanoContribuicao.Normal;

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var plano = (PlanoContribuicao)Opcao("plano");
        return _simulador.ExecutarAsync(new SimularContribuicaoSeguradoRequest(Competencia("competencia"),
            (CategoriaSegurado)Opcao("categoria"), plano, (PeriodicidadeContribuicao)Opcao("periodicidade"),
            plano == PlanoContribuicao.Normal ? Numero("salario") : 0m), cancellationToken);
    }
}
