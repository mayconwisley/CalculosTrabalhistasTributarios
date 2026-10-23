using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class IrpfAnualMobileViewModel : CalculadoraTrabalhistaViewModel
{
    // AliquotaNominalEmpresa usa a própria alíquota como valor (34, 40 e 45); a opção é convertida por posição.
    private static readonly AliquotaNominalEmpresa[] TiposEmpresa =
        [AliquotaNominalEmpresa.Geral, AliquotaNominalEmpresa.Financeira, AliquotaNominalEmpresa.Banco];

    private readonly ISimularDemonstrativoUseCase<SimularIrpfAnualRequest> _simulador;

    public override string Titulo => "IRPF anual";
    public override string Descricao => "Declaração do ano com a redução anual, os modelos completo e simplificado e a tributação mínima das altas rendas (Lei 15.270/2025).";

    public IrpfAnualMobileViewModel(ISimularDemonstrativoUseCase<SimularIrpfAnualRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<IrpfAnualMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("rendimentosSecao", "Rendimentos tributáveis");
        Adicionar("ano", "Ano-calendário", TipoCampoMobile.Inteiro, Math.Max(2026, DateTime.Today.Year).ToString(Cultura),
            "Ano dos rendimentos; a declaração é entregue no ano seguinte. Disponível desde 2026 (Lei 15.270/2025).");
        Adicionar("tributaveis", "Rendimentos tributáveis (R$)", TipoCampoMobile.Moeda, "0,00",
            "Salários, pró-labore, aluguéis e outros tributáveis no ajuste do ano, sem o 13º e a PLR.");
        Adicionar("aposentadoria", "Aposentadoria após 65 anos (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Aposentadoria e pensão a partir do mês dos 65 anos, sem o 13º, somando a parcela isenta e a tributável. Não as inclua nos rendimentos tributáveis.");
        Adicionar("mesesSessentaECinco", "Meses com 65 anos ou mais", TipoCampoMobile.Inteiro, "0",
            "Do mês do aniversário de 65 anos a dezembro, de 0 a 12. Cada mês isenta até R$ 1.903,98.");
        Adicionar("impostoPago", "Imposto retido ou pago (R$)", TipoCampoMobile.Moeda, "0,00",
            "IRRF sobre os tributáveis, carnê-leão e pagamentos complementares do ano.");

        Secao("deducoesSecao", "Deduções");
        Adicionar("previdencia", "Previdência oficial (R$)", TipoCampoMobile.Moeda, "0,00", "INSS ou regime próprio descontado no ano.");
        Adicionar("dependentes", "Dependentes", TipoCampoMobile.Inteiro, "0", "R$ 2.275,08 por dependente no modelo completo.");
        Adicionar("medicas", "Despesas médicas (R$)", TipoCampoMobile.Moeda, "0,00",
            "Sem limite no modelo completo, descontados os reembolsos.");
        Adicionar("instrucao", "Instrução (R$)", TipoCampoMobile.Moeda, "0,00",
            "Até R$ 3.561,50 por pessoa (titular e dependentes) no modelo completo.");
        Adicionar("pgbl", "Previdência privada PGBL (R$)", TipoCampoMobile.Moeda, "0,00",
            "PGBL e FAPI, até 12% dos tributáveis, para quem contribui para a previdência oficial.");
        Adicionar("pensao", "Pensão alimentícia paga (R$)", TipoCampoMobile.Moeda, "0,00",
            "Judicial, de acordo homologado ou de escritura pública; dedutível no modelo completo.");

        Secao("altasRendasSecao", "Tributação mínima das altas rendas");
        Adicionar("dividendos", "Dividendos recebidos (R$)", TipoCampoMobile.Moeda, "0,00", "Lucros e dividendos recebidos no ano.");
        Adicionar("irrfDividendos", "IRRF sobre dividendos (R$)", TipoCampoMobile.Moeda, "0,00",
            "Retenção de 10% sobre dividendos acima de R$ 50 mil no mês, compensada na tributação mínima.");
        Adicionar("outros", "Outros rendimentos (R$)", TipoCampoMobile.Moeda, "0,00",
            "Isentos ou exclusivos que entram na tributação mínima: 13º, PLR, JCP, aplicações. Não inclua poupança, LCI, LCA, heranças nem indenizações.");
        Adicionar("impostoExclusivo", "Imposto exclusivo pago (R$)", TipoCampoMobile.Moeda, "0,00",
            "Imposto exclusivo ou definitivo sobre os outros rendimentos, como o do 13º, da PLR e dos JCP.");
        Adicionar("aliquotaEmpresa", "Alíquota efetiva da empresa (%)", TipoCampoMobile.Numero, "0",
            "IRPJ e CSLL efetivos da empresa que pagou os dividendos, para o redutor; 0 para não calcular.");
        Adicionar("tipoEmpresa", "Tipo da empresa", TipoCampoMobile.Opcao,
            dica: "Alíquota nominal de IRPJ e CSLL que limita a soma das alíquotas efetivas (Lei 9.250/1995, art. 16-B).",
            opcoes: ["Em geral (34%)", "Seguradora ou financeira (40%)", "Banco (45%)"]);
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularIrpfAnualRequest(
            Inteiro("ano"), Numero("tributaveis"), Numero("previdencia"), Inteiro("dependentes"), Numero("medicas"),
            Numero("instrucao"), Numero("pgbl"), Numero("pensao"), Numero("impostoPago"), Numero("dividendos"),
            Numero("irrfDividendos"), Numero("outros"), Numero("impostoExclusivo"), Numero("aliquotaEmpresa"),
            TiposEmpresa[Opcao("tipoEmpresa")], Numero("aposentadoria"), Inteiro("mesesSessentaECinco")), cancellationToken);
}
