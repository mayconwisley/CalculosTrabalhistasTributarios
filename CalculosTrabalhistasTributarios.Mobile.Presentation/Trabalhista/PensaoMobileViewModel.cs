using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>Pensão alimentícia de um ou mais beneficiários e o IRRF de quem paga nas duas modalidades.</summary>
public sealed class PensaoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private const int MaximoBeneficiarios = 10;
    private const int BaseValorFixo = 3;
    private readonly ISimularPensaoUseCase _simulador;

    public override string Titulo => "Pensão alimentícia";
    public override string Descricao => "Calcule a pensão de um ou mais beneficiários e o efeito no IRRF e no líquido de quem paga.";

    public PensaoMobileViewModel(ISimularPensaoUseCase simulador, ICompartilhamentoPdf compartilhamento,
        ILogger<PensaoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Define as tabelas do INSS e do IRRF e o salário mínimo.");
        Adicionar("valorBruto", "Valor bruto (R$)", TipoCampoMobile.Moeda, "0,00",
            "Rendimentos de quem paga. Deixe 0,00 se a pensão não depende deles e você só quer o valor dela.");
        Adicionar("brutoComoBaseInss", "Base do INSS igual ao valor bruto?", TipoCampoMobile.SimNao,
            dica: "Desmarque para informar outra base, como a limitada a parte dos rendimentos.");
        Adicionar("baseInss", "Base de INSS (R$)", TipoCampoMobile.Moeda, "0,00", "O INSS respeita o teto da competência.");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0",
            "De 0 a 100. Quem recebe a pensão não pode ser deduzido também como dependente.");
        Adicionar("outrosDescontos", "Abatimentos dos rendimentos (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Valores retirados dos rendimentos antes do cálculo; não podem superar o valor bruto.");
        Adicionar("ordem", "Ordem das pensões", TipoCampoMobile.Opcao,
            dica: "Com mais de um beneficiário: todas sobre a mesma base, ou cada uma depois de descontar as anteriores.",
            opcoes: ["Todas sobre a mesma base", "Cada uma após descontar as anteriores"]);

        var beneficiarios = AdicionarGrade("beneficiarios", "Beneficiários",
            "Um por beneficiário, na ordem da decisão ou do acordo, até 10. Todas as pensões são deduzidas da base do IRRF na modalidade normal.",
            "Beneficiário", MaximoBeneficiarios,
            [
                new("nome", "Nome (opcional)", TipoCampoMobile.Texto, "", "Vazio usa o número da linha."),
                new("base", "Base da pensão", TipoCampoMobile.Opcao, Dica: "Sobre o que a pensão incide, conforme a decisão ou o acordo.",
                    Opcoes: ["% dos rendimentos líquidos", "% dos rendimentos brutos", "% do salário mínimo", "Valor fixo"]),
                new("percentual", "Percentual (%)", TipoCampoMobile.Numero, "0,00",
                    "Até 100 sobre os rendimentos; sobre o salário mínimo, até 1.000 (150 = 1,5 salário)."),
                new("valor", "Valor da pensão (R$)", TipoCampoMobile.Moeda, "0,00", "Valor fixo definido na decisão ou no acordo.")
            ],
            AtualizarLinha);
        beneficiarios.AdicionarLinha();
        beneficiarios.Alterada += (_, _) => AtualizarOrdem();
        Campo("brutoComoBaseInss").Marcado = true;
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "brutoComoBaseInss") Atualizar();
    }

    private void Atualizar()
    {
        Campo("baseInss").Visivel = !Sim("brutoComoBaseInss");
        AtualizarOrdem();
    }

    private void AtualizarOrdem()
    {
        if (Grades.Count > 0) Campo("ordem").Visivel = Grade("beneficiarios").Linhas.Count > 1;
    }

    private static void AtualizarLinha(LinhaGradeMobile linha)
    {
        var valorFixo = linha.Campo("base").Indice == BaseValorFixo;
        linha.Campo("percentual").Visivel = !valorFixo;
        linha.Campo("valor").Visivel = valorFixo;
    }

    protected override async Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var competencia = Competencia("competencia");
        var valorBruto = Numero("valorBruto");
        // Com a opção marcada, a base acompanha o valor bruto, como a sincronização da janela do desktop.
        var baseInss = Sim("brutoComoBaseInss") ? valorBruto : Numero("baseInss");
        var dependentes = Inteiro("dependentes");
        var outrosDescontos = Numero("outrosDescontos");
        var beneficiarios = Grade("beneficiarios").Ler(LerBeneficiario);
        var sucessiva = beneficiarios.Count > 1 && Opcao("ordem") == 1;

        var resultado = await _simulador.ExecutarAsync(
            new SimularPensaoRequest(competencia, valorBruto, baseInss, dependentes, beneficiarios, outrosDescontos, sucessiva), cancellationToken);
        var entrada = new EntradaPensaoDto(competencia, valorBruto, baseInss, dependentes, beneficiarios, outrosDescontos, sucessiva);
        return resultado.Map(simulacao => DemonstrativoPensaoAlimenticia.Montar(simulacao, entrada));
    }

    private static BeneficiarioPensao LerBeneficiario(LinhaGradeMobile linha)
    {
        var nome = linha.Campo("nome").Valor.Trim();
        var regra = LeituraCampoMobile.Opcao(linha.Campo("base")) switch
        {
            0 => new RegraPensao(BasePensao.RendimentosLiquidos, LeituraCampoMobile.Numero(linha.Campo("percentual")), 0m),
            1 => new RegraPensao(BasePensao.RendimentosBrutos, LeituraCampoMobile.Numero(linha.Campo("percentual")), 0m),
            2 => new RegraPensao(BasePensao.SalarioMinimo, LeituraCampoMobile.Numero(linha.Campo("percentual")), 0m),
            _ => new RegraPensao(BasePensao.ValorFixo, 0m, LeituraCampoMobile.Numero(linha.Campo("valor")))
        };
        // A validação da regra identifica a linha, como a mensagem com o nome do beneficiário no desktop.
        if (regra.Validar() is { Falhou: true } invalida)
            throw new EntradaInvalidaException(invalida.Erro.Mensagem);
        return new BeneficiarioPensao(nome.Length == 0 ? linha.Titulo : nome, regra);
    }
}
