using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class PensaoMorteMobileViewModel : CalculadoraTrabalhistaViewModel
{
    // O aposentado já cumpriu a carência da aposentadoria: tem bem mais que as 18 contribuições exigidas para a duração da
    // pensão. Mesmo valor que o desktop envia.
    private const int ContribuicoesDoAposentado = 180;

    private readonly ISimularDemonstrativoUseCase<SimularPensaoMorteRequest> _simulador;
    private readonly GradeMobile _salarios;

    public override string Titulo => "Pensão por morte";
    public override string Descricao => "Calcule a pensão por morte do INSS: cota familiar, valor por dependente, duração para o cônjuge e o redutor da acumulação.";

    public PensaoMorteMobileViewModel(ISimularDemonstrativoUseCase<SimularPensaoMorteRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<PensaoMorteMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("secaoSegurado", "Segurado");
        Adicionar("dataObito", "Data do óbito", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Define o salário mínimo, o teto e a tabela de duração da pensão do cônjuge.");
        Adicionar("situacao", "Situação do segurado", TipoCampoMobile.Opcao,
            dica: "Aposentado: a pensão parte da aposentadoria que recebia. Não aposentado: da aposentadoria por incapacidade permanente a que teria direito.",
            opcoes: ["Aposentado", "Não aposentado"]);
        Adicionar("aposentadoria", "Aposentadoria recebida (R$)", TipoCampoMobile.Moeda, "0,00", "Valor mensal da aposentadoria do segurado na data do óbito.");
        Adicionar("sexo", "Sexo do segurado", TipoCampoMobile.Opcao,
            dica: "Define o tempo a partir do qual o coeficiente cresce: 20 anos para o homem e 15 para a mulher.",
            opcoes: ["Homem", "Mulher"]);
        Adicionar("anos", "Contribuição: anos", TipoCampoMobile.Inteiro, "0", "Tempo de contribuição do segurado na data do óbito, em anos completos.");
        Adicionar("meses", "Contribuição: meses (0 a 11)", TipoCampoMobile.Inteiro, "0", "Os meses além dos anos completos.");
        Adicionar("causa", "Causa do óbito", TipoCampoMobile.Opcao,
            dica: "O acidente dispensa as 18 contribuições e os 2 anos de união na duração da cota do cônjuge; o do trabalho também leva a base a 100% da média.",
            opcoes: ["Doença ou causa comum", "Acidente fora do trabalho", "Acidente do trabalho ou doença profissional"]);
        Secao("secaoDependentes", "Dependentes");
        Adicionar("conjuge", "Deixou cônjuge?", TipoCampoMobile.SimNao,
            dica: "Cônjuge ou companheiro em união estável, inclusive o ex-cônjuge com pensão alimentícia.").Marcado = true;
        Adicionar("idadeConjuge", "Idade do cônjuge no óbito (anos)", TipoCampoMobile.Inteiro, "45", "Anos completos na data do óbito: define a duração da cota dele.");
        Adicionar("uniao", "União de 2 anos ou mais?", TipoCampoMobile.SimNao,
            dica: "Com menos de 2 anos de casamento ou união, a cota do cônjuge dura 4 meses, salvo óbito por acidente.").Marcado = true;
        Adicionar("beneficioConjuge", "Benefício do cônjuge (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Aposentadoria ou pensão que o próprio cônjuge já recebe, para o redutor da acumulação. Deixe 0,00 se não houver.");
        Adicionar("outros", "Outros dependentes", TipoCampoMobile.Inteiro, "0", "Filhos até 21 anos ou inválidos e os demais dependentes, além do cônjuge.");
        Adicionar("invalido", "Dependente inválido?", TipoCampoMobile.SimNao,
            dica: "Inválido ou com deficiência intelectual, mental ou grave: a pensão é de 100% (EC 103/2019, art. 23, § 2º).");
        Secao("secaoMedia", "Média dos salários");
        Adicionar(SalariosContribuicaoMobile.ChaveOrigem, "Origem da média", TipoCampoMobile.Opcao,
            dica: "Calcular: informe na grade os salários do segurado desde 07/1994, corrigidos pelo INPC até o óbito. Informar: use a média já apurada.",
            opcoes: SalariosContribuicaoMobile.OpcoesOrigem);
        Adicionar(SalariosContribuicaoMobile.ChaveMedia, "Média informada (R$)", TipoCampoMobile.Moeda, "0,00", SalariosContribuicaoMobile.DicaMedia);
        _salarios = AdicionarGrade(SalariosContribuicaoMobile.ChaveGrade, SalariosContribuicaoMobile.Titulo,
            SalariosContribuicaoMobile.Descricao("o mês anterior ao do óbito"), SalariosContribuicaoMobile.NomeLinha,
            CalculadoraMediaSalarios.LimitePeriodos, SalariosContribuicaoMobile.Colunas);
        SalariosContribuicaoMobile.PreencherInicioDosNovos(_salarios);
        Atualizar();
    }

    private bool Aposentado => (SituacaoInstituidor)Opcao("situacao") == SituacaoInstituidor.Aposentado;
    private bool CalcularMedia => Opcao(SalariosContribuicaoMobile.ChaveOrigem) == 0;

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave is "situacao" or "conjuge" or SalariosContribuicaoMobile.ChaveOrigem) Atualizar();
    }

    private void Atualizar()
    {
        if (_salarios is null) return;
        var aposentado = Aposentado;
        Campo("aposentadoria").Visivel = aposentado;
        Campo("sexo").Visivel = Campo("anos").Visivel = Campo("meses").Visivel = !aposentado;
        Campo("secaoMedia").Visivel = Campo(SalariosContribuicaoMobile.ChaveOrigem).Visivel = !aposentado;
        _salarios.Visivel = !aposentado && CalcularMedia;
        Campo(SalariosContribuicaoMobile.ChaveMedia).Visivel = !aposentado && !CalcularMedia;
        Campo("idadeConjuge").Visivel = Campo("uniao").Visivel = Campo("beneficioConjuge").Visivel = Sim("conjuge");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var aposentado = Aposentado;
        var conjuge = Sim("conjuge");
        var mesesContribuicao = ContribuicoesDoAposentado;
        if (!aposentado)
        {
            var meses = Inteiro("meses");
            if (meses is < 0 or > 11)
                throw new EntradaInvalidaException($"O campo \"{Campo("meses").Rotulo}\" aceita de 0 a 11 meses; some os anos completos no campo de anos.");
            mesesContribuicao = Inteiro("anos") * 12 + meses;
        }
        var salarios = !aposentado && CalcularMedia ? SalariosContribuicaoMobile.Ler(_salarios) : null;
        return _simulador.ExecutarAsync(new SimularPensaoMorteRequest(
            Data("dataObito"), (SituacaoInstituidor)Opcao("situacao"),
            aposentado ? Numero("aposentadoria") : 0m, aposentado || CalcularMedia ? 0m : Numero(SalariosContribuicaoMobile.ChaveMedia),
            (SexoSegurado)Opcao("sexo"), mesesContribuicao, (CausaObito)Opcao("causa"),
            conjuge, conjuge ? Inteiro("idadeConjuge") : 0, conjuge && Sim("uniao"), Inteiro("outros"), Sim("invalido"),
            conjuge ? Numero("beneficioConjuge") : 0m, salarios), cancellationToken);
    }
}
