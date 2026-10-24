using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class GanhoCapitalMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularGanhoCapitalRequest> _simulador;

    public override string Titulo => "Ganho de capital";
    public override string Descricao => "Calcule o IR na venda de um imóvel ou de outro bem, com as isenções e os fatores de redução.";

    public GanhoCapitalMobileViewModel(ISimularDemonstrativoUseCase<SimularGanhoCapitalRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<GanhoCapitalMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        // Mesma ordem de BemAlienado, para converter o índice no enum.
        Adicionar("bem", "Bem vendido", TipoCampoMobile.Opcao,
            dica: "Imóveis têm fatores de redução pelo tempo de posse; o residencial pode ter isenção pelo reinvestimento.",
            opcoes: ["Imóvel residencial", "Outro imóvel (comercial, terreno)", "Outro bem (veículo, participação, outros)"]);
        Adicionar("aquisicao", "Data da compra (dd/mm/aaaa)", TipoCampoMobile.Data, DateTime.Today.AddYears(-10).ToString("dd/MM/yyyy", Cultura),
            "Data da escritura ou do contrato de compra.");
        Adicionar("venda", "Data da venda (dd/mm/aaaa)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Data do contrato de venda.");
        Adicionar("custo", "Custo de aquisição (R$)", TipoCampoMobile.Moeda, "0,00", "Valor de compra declarado no IRPF, com as benfeitorias.");
        Adicionar("valorVenda", "Valor da venda (R$)", TipoCampoMobile.Moeda, "0,00", "Valor total da venda.");
        Adicionar("despesas", "Despesas da venda (R$)", TipoCampoMobile.Moeda, "0,00", "Corretagem e outras despesas da venda pagas por você.");
        Adicionar("unico", "Único imóvel?", TipoCampoMobile.SimNao,
            dica: "Seu único imóvel, sem outra venda de imóvel nos últimos 5 anos: isento até R$ 440.000,00.");
        Adicionar("reinvestido", "Reinvestido em 180 dias (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor da venda aplicado em outro imóvel residencial no País em até 180 dias.");
        Adicionar("vendasNoMes", "Vendas no mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Total vendido no mês em bens da mesma natureza, para a isenção de até R$ 35.000,00; 0,00 considera só esta venda.");
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "bem") Atualizar();
    }

    private void Atualizar()
    {
        var bem = (BemAlienado)Campo("bem").Indice;
        Campo("unico").Visivel = bem != BemAlienado.OutroBem;
        Campo("reinvestido").Visivel = bem == BemAlienado.ImovelResidencial;
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var bem = (BemAlienado)Opcao("bem");
        var unico = bem != BemAlienado.OutroBem && Sim("unico");
        var reinvestido = bem == BemAlienado.ImovelResidencial ? Numero("reinvestido") : 0m;
        return _simulador.ExecutarAsync(new SimularGanhoCapitalRequest(bem, Data("aquisicao"), Data("venda"), Numero("custo"),
            Numero("valorVenda"), Numero("despesas"), unico, reinvestido, Numero("vendasNoMes")), cancellationToken);
    }
}
