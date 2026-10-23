using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>
/// Atualização de débitos judiciais trabalhistas e cíveis, como a janela do desktop: parcelas com vencimento e valor, as
/// datas do processo e os critérios da decisão. Os índices vêm do banco local; o aplicativo não os atualiza pela internet.
/// </summary>
public sealed class DebitoJudicialMobileViewModel : CalculadoraTrabalhistaViewModel
{
    /// <summary>Mesmo limite do caso de uso.</summary>
    private const int LimiteParcelas = 600;
    private readonly ISimularDebitoJudicialUseCase _simulador;

    public override string Titulo => "Débitos judiciais";
    public override string Descricao => "Atualize débitos trabalhistas e cíveis com correção e juros pelas fases da ADC 58, do TST, do STJ e da Lei 14.905/2024.";

    public DebitoJudicialMobileViewModel(ISimularDebitoJudicialUseCase simulador, ICompartilhamentoPdf compartilhamento,
        ILogger<DebitoJudicialMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("natureza", "Natureza do débito", TipoCampoMobile.Opcao,
            dica: "Trabalhista: IPCA-E e TR antes do ajuizamento, Selic até 29/08/2024 e IPCA com taxa legal depois. Cível: o índice da decisão, a Selic com os juros até 29/08/2024 e IPCA com taxa legal depois.",
            opcoes: ["Trabalhista", "Cível"]);
        Adicionar("dataCalculo", "Data do cálculo (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Data até a qual o débito é atualizado.");
        Adicionar("ajuizamento", "Data do ajuizamento (opcional)", TipoCampoMobile.Data,
            dica: "Data em que a ação foi proposta (dd/MM/yyyy). Vazia: todo o período é pré-judicial, com IPCA-E e TR.");
        Adicionar("inicioJuros", "Juros", TipoCampoMobile.Opcao,
            dica: "Mora desde o vencimento (Código Civil, art. 397) ou desde a citação (art. 405), conforme a decisão.",
            opcoes: ["Do vencimento", "Da citação"]);
        Adicionar("citacao", "Data da citação (dd/MM/yyyy)", TipoCampoMobile.Data, dica: "Início dos juros das parcelas vencidas até ela.");
        Adicionar("indice", "Índice até 29/08/2024", TipoCampoMobile.Opcao,
            dica: "Índice da decisão ou da tabela do tribunal para a correção antes dos juros e antes de 30/08/2024; depois, vale o IPCA (Código Civil, art. 389).",
            opcoes: ["INPC", "IPCA"]);
        Adicionar("acrescimos", "Multa e honorários (art. 523)", TipoCampoMobile.Opcao,
            dica: "No cumprimento de sentença, sem pagamento em 15 dias úteis da intimação, incidem multa de 10% e honorários de 10% (CPC, art. 523, § 1º); os honorários são calculados sem a multa.",
            opcoes: ["Não incluir", "Multa de 10% e honorários de 10%", "Só a multa de 10%"]);
        var parcelas = AdicionarGrade("parcelas", "Parcelas", "Informe a descrição, o vencimento e o valor de cada parcela.", "Parcela", LimiteParcelas,
        [
            new ColunaGradeMobile("descricao", "Descrição (opcional)", TipoCampoMobile.Texto, Dica: "Vazia: Parcela 1, Parcela 2..."),
            new ColunaGradeMobile("vencimento", "Vencimento (dd/MM/yyyy)", TipoCampoMobile.Data),
            new ColunaGradeMobile("valor", "Valor (R$)", TipoCampoMobile.Moeda, "0,00")
        ]);
        parcelas.AdicionarLinha(new Dictionary<string, string> { ["descricao"] = "Parcela 1" });
        AtualizarVisibilidade();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave is "natureza" or "inicioJuros")
            AtualizarVisibilidade();
    }

    private bool EhCivel => Campo("natureza").Indice == (int)NaturezaDebito.Civel;
    private bool UsaCitacao => EhCivel && Campo("inicioJuros").Indice == (int)InicioJurosCivel.Citacao;

    private void AtualizarVisibilidade()
    {
        Campo("ajuizamento").Visivel = !EhCivel;
        Campo("inicioJuros").Visivel = EhCivel;
        Campo("citacao").Visivel = UsaCitacao;
        Campo("indice").Visivel = EhCivel;
        Campo("acrescimos").Visivel = EhCivel;
    }

    protected override async Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var natureza = (NaturezaDebito)Opcao("natureza");
        var civel = natureza == NaturezaDebito.Civel;
        var calculo = Data("dataCalculo");
        var ajuizamento = civel ? null : DataOpcional("ajuizamento");
        DateOnly? citacao = UsaCitacao ? Data("citacao") : null;
        var numero = 0;
        var parcelas = Grade("parcelas").Ler(linha => LerParcela(linha, ++numero));
        var pedido = new SimularDebitoJudicialRequest(natureza, parcelas, calculo, ajuizamento, (InicioJurosCivel)Opcao("inicioJuros"), citacao,
            (IndiceCivelAnterior)Opcao("indice"), civel ? (AcrescimosPenhora)Opcao("acrescimos") : AcrescimosPenhora.Nenhum);
        var simulacao = await _simulador.CalcularAsync(pedido, cancellationToken);
        if (simulacao.Falhou)
            return simulacao.Erro;
        return DemonstrativoDebitoJudicial.Montar(simulacao.Valor);
    }

    /// <summary>Descrição vazia vira "Parcela N", como no desktop; valor e vencimento são obrigatórios.</summary>
    private static ParcelaDebito LerParcela(LinhaGradeMobile linha, int numero)
    {
        var descricao = linha.Campo("descricao").Valor.Trim();
        return new ParcelaDebito(descricao.Length == 0 ? $"Parcela {numero}" : descricao,
            LeituraCampoMobile.Data(linha.Campo("vencimento")), LeituraCampoMobile.Numero(linha.Campo("valor")));
    }
}
