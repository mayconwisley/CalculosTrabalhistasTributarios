using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class AfastamentoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularAfastamentoRequest> _simulador;

    public override string Titulo => "Afastamentos e licenças";
    public override string Descricao => "Veja quanto a empresa e o INSS pagam na doença, no acidente de trabalho e nas licenças-maternidade e paternidade.";

    public AfastamentoMobileViewModel(ISimularDemonstrativoUseCase<SimularAfastamentoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<AfastamentoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        // Mesma ordem de TipoAfastamento.
        Adicionar("tipo", "Tipo", TipoCampoMobile.Opcao,
            dica: "Na doença e no acidente, a empresa paga os 15 primeiros dias e o INSS, o restante.",
            opcoes: ["Doença", "Acidente de trabalho", "Licença-maternidade", "Licença-paternidade"]);
        Adicionar("inicio", "Início (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Primeiro dia do afastamento ou da licença.");
        Adicionar("dias", "Dias de afastamento", TipoCampoMobile.Inteiro, "30",
            "Dias do atestado ou da perícia, contando o primeiro.");
        Adicionar("remuneracao", "Remuneração mensal (R$)", TipoCampoMobile.Moeda, "0,00",
            "Salário mensal com os adicionais fixos.");
        Adicionar("media", "Média dos salários de contribuição (R$, opcional)", TipoCampoMobile.Moeda, "0,00",
            "Usada para estimar o auxílio do INSS; 0,00 usa a remuneração.");
        Adicionar("anterior", "Afastamento anterior pelo mesmo motivo", TipoCampoMobile.Opcao,
            dica: "Novo afastamento em até 60 dias do retorno ou da cessação do benefício soma os dias, e a empresa não paga de novo os 15 dias (Decreto 3.048/1999, art. 75).",
            opcoes: ["Não houve", "Pago pela empresa", "Benefício do INSS"]);
        Adicionar("fimAnterior", "Fim do afastamento anterior (dd/MM/yyyy)", TipoCampoMobile.Data, "",
            "Último dia do afastamento ou do benefício anterior.");
        Adicionar("diasAnteriores", "Dias pagos pela empresa no anterior", TipoCampoMobile.Inteiro, "0",
            "De 1 a 15 dias.");
        Adicionar("empresaCidada", "Empresa Cidadã", TipoCampoMobile.SimNao,
            dica: "+60 dias de licença-maternidade e +15 de licença-paternidade.");
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave is "tipo" or "anterior") Atualizar();
    }

    private void Atualizar()
    {
        var licenca = Campo("tipo").Indice is 2 or 3;
        Campo("dias").Visivel = Campo("media").Visivel = Campo("anterior").Visivel = !licenca;
        Campo("empresaCidada").Visivel = licenca;
        var origem = Campo("anterior").Indice;
        Campo("fimAnterior").Visivel = !licenca && origem > 0;
        Campo("diasAnteriores").Visivel = !licenca && origem == 1;
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var anterior = Campo("fimAnterior").Visivel
            ? new AfastamentoAnterior(Opcao("anterior") == 2 ? OrigemAfastamentoAnterior.BeneficioInss : OrigemAfastamentoAnterior.Empresa,
                Data("fimAnterior"), Campo("diasAnteriores").Visivel ? Inteiro("diasAnteriores") : 0)
            : null;
        return _simulador.ExecutarAsync(new SimularAfastamentoRequest((TipoAfastamento)Opcao("tipo"), Data("inicio"),
            Campo("dias").Visivel ? Inteiro("dias") : 0, Numero("remuneracao"), Campo("media").Visivel ? Numero("media") : 0m,
            Campo("empresaCidada").Visivel && Sim("empresaCidada"), anterior), cancellationToken);
    }
}
