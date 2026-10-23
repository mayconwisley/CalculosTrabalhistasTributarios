using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class DecimoTerceiroMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularDecimoTerceiroRequest> _parcelas;
    private readonly ISimularDemonstrativoUseCase<SimularComplementoDecimoTerceiroRequest> _complemento;
    public override string Titulo => "13º salário";
    public override string Descricao => "1ª e 2ª parcelas ou complemento das médias de dezembro. O INSS e o IRRF seguem a competência informada.";

    public DecimoTerceiroMobileViewModel(ISimularDemonstrativoUseCase<SimularDecimoTerceiroRequest> parcelas,
        ISimularDemonstrativoUseCase<SimularComplementoDecimoTerceiroRequest> complemento,
        ICompartilhamentoPdf compartilhamento, ILogger<DecimoTerceiroMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _parcelas = parcelas;
        _complemento = complemento;
        Adicionar("calculo", "Cálculo", TipoCampoMobile.Opcao, opcoes: ["1ª e 2ª parcelas", "Complemento das médias (até 10/01)"]);
        Adicionar("competencia", "Competência do pagamento (MM/yyyy)", TipoCampoMobile.Competencia, $"12/{DateTime.Today.Year}");
        Adicionar("salario", "Salário mensal (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("medias", "Médias de variáveis (R$)", TipoCampoMobile.Moeda, "0,00", "Média anual de horas extras, comissões e adicionais.");
        Adicionar("avos", "Avos (meses trabalhados)", TipoCampoMobile.Inteiro, "12", "De 1 a 12; cada mês com pelo menos 15 dias conta um avo.");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
        Adicionar("adiantamento", "1ª parcela", TipoCampoMobile.Opcao, opcoes: ["50% do 13º", "Não houve adiantamento", "Valor informado"]);
        Adicionar("valorAdiantamento", "Valor da 1ª parcela (R$)", TipoCampoMobile.Moeda, "0,00");
        AdicionarPensao();
        Adicionar("previdencia", "Previdência complementar (R$)", TipoCampoMobile.Moeda, "0,00", "Contribuição sobre o 13º, deduzida do IRRF até o limite da regra.");
        Adicionar("variaveisAteNovembro", "Variáveis de janeiro a novembro (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("variaveisDezembro", "Variáveis de dezembro (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("pagamento", "Pagamento do complemento (MM/yyyy)", TipoCampoMobile.Competencia, $"01/{DateTime.Today.Year + 1}");
        Adicionar("tributacao", "IRRF no ano seguinte", TipoCampoMobile.Opcao,
            opcoes: ["RRA de 1 mês (eSocial)", "Recálculo do 13º (IN 1.500)"]);
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave is "calculo" or "adiantamento" or "pensaoBase") Atualizar();
    }

    private void Atualizar()
    {
        var complemento = Campo("calculo").Indice == 1;
        Campo("adiantamento").Visivel = !complemento;
        Campo("valorAdiantamento").Visivel = !complemento && Campo("adiantamento").Indice == 2;
        Campo("pensaoBase").Visivel = !complemento;
        Campo("previdencia").Visivel = !complemento;
        foreach (var chave in new[] { "variaveisAteNovembro", "variaveisDezembro", "pagamento", "tributacao" })
            Campo(chave).Visivel = complemento;
        AtualizarPensao();
        if (complemento)
        {
            Campo("pensaoPercentual").Visivel = false;
            Campo("pensaoValor").Visivel = false;
        }
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var competencia = Competencia("competencia");
        var salario = Numero("salario");
        var medias = Numero("medias");
        var avos = Inteiro("avos");
        var dependentes = Inteiro("dependentes");
        if (Opcao("calculo") == 1)
            return _complemento.ExecutarAsync(new SimularComplementoDecimoTerceiroRequest(
                competencia, Competencia("pagamento"), salario, medias, Numero("variaveisAteNovembro"),
                Numero("variaveisDezembro"), avos, dependentes, (TributacaoComplementoDecimoTerceiro)Opcao("tributacao")), cancellationToken);
        return _parcelas.ExecutarAsync(new SimularDecimoTerceiroRequest(competencia, salario, medias, avos, dependentes,
            (AdiantamentoDecimoTerceiro)Opcao("adiantamento"), Campo("adiantamento").Indice == 2 ? Numero("valorAdiantamento") : 0m,
            Pensao(), Numero("previdencia")), cancellationToken);
    }
}
