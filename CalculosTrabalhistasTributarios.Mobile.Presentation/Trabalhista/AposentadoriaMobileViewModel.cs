using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class AposentadoriaMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularAposentadoriaRequest> _simulador;
    private readonly GradeMobile _salarios;

    public override string Titulo => "Simulação de aposentadoria";
    public override string Descricao => "Veja quando você cumpre cada regra de aposentadoria do INSS da reforma de 2019 e o benefício estimado em cada uma.";

    public AposentadoriaMobileViewModel(ISimularDemonstrativoUseCase<SimularAposentadoriaRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<AposentadoriaMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("sexo", "Sexo", TipoCampoMobile.Opcao,
            dica: "Os requisitos de idade e de tempo e o coeficiente do benefício são diferentes para homens e mulheres.",
            opcoes: ["Homem", "Mulher"]);
        Adicionar("nascimento", "Data de nascimento", TipoCampoMobile.Data, DateTime.Today.AddYears(-50).ToString("dd/MM/yyyy", Cultura));
        Adicionar("dataSimulacao", "Data da simulação", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Data a que se refere o tempo de contribuição; a projeção começa nela, com o salário mínimo e o teto do mês.");
        Secao("secaoTempo", "Tempo de contribuição");
        Adicionar("anos", "Até a data: anos", TipoCampoMobile.Inteiro, "0", "Anos completos de contribuição até a data da simulação, como no CNIS.");
        Adicionar("meses", "Até a data: meses (0 a 11)", TipoCampoMobile.Inteiro, "0", "Os meses além dos anos completos.");
        Adicionar("anosReforma", "Em 13/11/2019: anos", TipoCampoMobile.Inteiro, "0",
            "Tempo até a data da reforma. Use 0 se começou a contribuir depois dela.");
        Adicionar("mesesReforma", "Em 13/11/2019: meses (0 a 11)", TipoCampoMobile.Inteiro, "0", "Os meses além dos anos completos em 13/11/2019.");
        Adicionar("continua", "Continua contribuindo?", TipoCampoMobile.SimNao,
            dica: "Sim: o tempo cresce um mês a cada mês da projeção. Não: só a idade avança.").Marcado = true;
        Adicionar("expectativa", "Sobrevida do IBGE (anos, opcional)", TipoCampoMobile.Numero, "",
            "Expectativa de sobrevida na idade da aposentadoria, pela tábua do IBGE, para o fator previdenciário do pedágio de 50%. Deixe vazio se não souber.");
        Secao("secaoMedia", "Média dos salários");
        Adicionar(SalariosContribuicaoMobile.ChaveOrigem, "Origem da média", TipoCampoMobile.Opcao,
            dica: "Calcular: informe na grade os salários desde 07/1994, corrigidos pelo INPC até a data da simulação. Informar: use a média do simulador do Meu INSS.",
            opcoes: SalariosContribuicaoMobile.OpcoesOrigem);
        Adicionar(SalariosContribuicaoMobile.ChaveMedia, "Média informada (R$)", TipoCampoMobile.Moeda, "0,00", SalariosContribuicaoMobile.DicaMedia);
        _salarios = AdicionarGrade(SalariosContribuicaoMobile.ChaveGrade, SalariosContribuicaoMobile.Titulo,
            SalariosContribuicaoMobile.Descricao("a data da simulação"), SalariosContribuicaoMobile.NomeLinha,
            CalculadoraMediaSalarios.LimitePeriodos, SalariosContribuicaoMobile.Colunas);
        SalariosContribuicaoMobile.PreencherInicioDosNovos(_salarios);
        Atualizar();
    }

    private bool CalcularMedia => Opcao(SalariosContribuicaoMobile.ChaveOrigem) == 0;

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == SalariosContribuicaoMobile.ChaveOrigem) Atualizar();
    }

    private void Atualizar()
    {
        _salarios.Visivel = CalcularMedia;
        Campo(SalariosContribuicaoMobile.ChaveMedia).Visivel = !CalcularMedia;
    }

    private int Meses(string chave)
    {
        var meses = Inteiro(chave);
        if (meses is < 0 or > 11)
            throw new EntradaInvalidaException($"O campo \"{Campo(chave).Rotulo}\" aceita de 0 a 11 meses; some os anos completos no campo de anos.");
        return meses;
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var salarios = CalcularMedia ? SalariosContribuicaoMobile.Ler(_salarios) : null;
        var meses = Meses("meses");
        var mesesReforma = Meses("mesesReforma");
        return _simulador.ExecutarAsync(new SimularAposentadoriaRequest(
            (SexoSegurado)Opcao("sexo"), Data("nascimento"), Data("dataSimulacao"),
            Inteiro("anos") * 12 + meses, Inteiro("anosReforma") * 12 + mesesReforma, Sim("continua"),
            CalcularMedia ? 0m : Numero(SalariosContribuicaoMobile.ChaveMedia),
            Texto("expectativa").Length == 0 ? null : Numero("expectativa"), salarios), cancellationToken);
    }
}
