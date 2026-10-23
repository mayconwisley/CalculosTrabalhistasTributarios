using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>Indenização do período de estabilidade restante, com 13º, férias com 1/3, FGTS e multa de 40%.</summary>
public sealed class EstabilidadeMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularEstabilidadeUseCase _simulador;

    public override string Titulo => "Estabilidade";
    public override string Descricao => "Estime a indenização do período de estabilidade que falta após a demissão.";

    public EstabilidadeMobileViewModel(ISimularEstabilidadeUseCase simulador, ICompartilhamentoPdf compartilhamento,
        ILogger<EstabilidadeMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        var hoje = DateTime.Today;
        Adicionar("media", "Média remuneratória (R$)", TipoCampoMobile.Moeda, "0,00", "Média da remuneração usada como referência para a indenização.");
        Adicionar("diasBase", "Dias-base", TipoCampoMobile.Inteiro, "30", "Divisor da média diária para os dias que sobram além dos meses cheios. O padrão é 30.");
        Adicionar("demissao", "Demissão (dd/MM/yyyy)", TipoCampoMobile.Data, hoje.ToString("dd/MM/yyyy", Cultura));
        Adicionar("fimEstabilidade", "Fim da estabilidade (dd/MM/yyyy)", TipoCampoMobile.Data, hoje.AddDays(30).ToString("dd/MM/yyyy", Cultura),
            "Último dia do período de estabilidade; deve ser posterior à demissão.");
        Adicionar("complementos", "Complementos (R$, opcional)", TipoCampoMobile.Moeda, "0,00", "Valores adicionais somados ao total, se houver.");
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var pedido = new SimularEstabilidadeRequest(Numero("media"), Inteiro("diasBase"), Data("demissao"), Data("fimEstabilidade"), Numero("complementos"));
        var entrada = new EntradaEstabilidadeDto(pedido.MediaRemuneratoria, pedido.DiasBase, pedido.Demissao, pedido.FimEstabilidade, pedido.Complementos);
        return Task.FromResult(_simulador.Executar(pedido).Map(simulacao => DemonstrativoEstabilidade.Montar(simulacao, entrada)));
    }
}
