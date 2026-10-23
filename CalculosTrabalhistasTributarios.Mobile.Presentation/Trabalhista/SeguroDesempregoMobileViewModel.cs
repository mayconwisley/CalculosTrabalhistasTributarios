using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class SeguroDesempregoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularSeguroDesempregoRequest> _simulador;

    public override string Titulo => "Seguro-desemprego";
    public override string Descricao => "Estime o valor e a quantidade de parcelas pela média dos últimos salários.";

    public SeguroDesempregoMobileViewModel(ISimularDemonstrativoUseCase<SimularSeguroDesempregoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<SeguroDesempregoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("vinculo", "Vínculo", TipoCampoMobile.Opcao,
            dica: "Doméstico: uma parcela de um salário mínimo, até 3 parcelas, conforme a LC 150/2015.",
            opcoes: ["Empregado (CLT)", "Empregado doméstico"]);
        Adicionar("dispensa", "Data da dispensa (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Define a tabela do benefício.");
        Adicionar("ultimo", "Salário do último mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Mês anterior à dispensa, incluindo horas extras e adicionais.");
        Adicionar("penultimo", "Salário do penúltimo mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Use 0,00 se não houve salário nesse mês; ele fica fora da média.");
        Adicionar("antepenultimo", "Salário do antepenúltimo mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Use 0,00 se não houve salário nesse mês; ele fica fora da média.");
        Adicionar("meses", "Meses trabalhados", TipoCampoMobile.Inteiro, "12",
            "CLT: meses com salário nos últimos 36; doméstico: meses como doméstico nos últimos 24.");
        Adicionar("solicitacao", "Solicitação", TipoCampoMobile.Opcao,
            dica: "Conte esta solicitação.", opcoes: ["1ª solicitação", "2ª solicitação", "3ª ou seguinte"]);
        Adicionar("meses18", "Meses com salário nos últimos 18", TipoCampoMobile.Inteiro, "",
            "Na 1ª solicitação, são exigidos pelo menos 12 dos 18 meses.");
        Adicionar("meses12", "Meses com salário nos últimos 12", TipoCampoMobile.Inteiro, "",
            "Na 2ª solicitação, são exigidos pelo menos 9 dos 12 meses.");
        Adicionar("meses6", "Meses com salário nos últimos 6", TipoCampoMobile.Inteiro, "",
            "Da 3ª solicitação em diante, é exigido salário em todos os 6 meses.");
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave is "vinculo" or "solicitacao") Atualizar();
    }

    private void Atualizar()
    {
        var domestico = Opcao("vinculo") == 1;
        foreach (var chave in new[] { "ultimo", "penultimo", "antepenultimo", "solicitacao" })
            Campo(chave).Visivel = !domestico;
        Campo("meses18").Visivel = !domestico && Opcao("solicitacao") == 0;
        Campo("meses12").Visivel = !domestico && Opcao("solicitacao") == 1;
        Campo("meses6").Visivel = !domestico && Opcao("solicitacao") == 2;
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var domestico = Opcao("vinculo") == 1;
        var solicitacao = (SolicitacaoSeguroDesemprego)Opcao("solicitacao");
        var carencia = domestico ? null : Opcao("solicitacao") switch
        {
            0 => (int?)Inteiro("meses18"),
            1 => Inteiro("meses12"),
            _ => Inteiro("meses6")
        };
        var salarios = domestico ? new decimal[] { 0m, 0m, 0m } :
            [Numero("ultimo"), Numero("penultimo"), Numero("antepenultimo")];
        return _simulador.ExecutarAsync(new SimularSeguroDesempregoRequest(Data("dispensa"), salarios, solicitacao,
            Inteiro("meses"), domestico, carencia), cancellationToken);
    }
}
