using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraRra : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularRraRequest> _simulador;
    private readonly CampoTextoViewModel _dataPagamento = new("Data do pagamento", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
        "Dia do recebimento ou crédito (dd/mm/aaaa). Define as tabelas e separa os anos anteriores do ano do pagamento.");
    private readonly CampoOpcaoViewModel _origem = new("Quem paga",
        [new("Fonte pagadora ou Justiça do Trabalho", OrigemPagamentoRra.FontePagadora), new("Justiça Federal (precatório ou RPV)", OrigemPagamentoRra.JusticaFederal)],
        "Empregador, INSS, Justiça do Trabalho ou Estadual usam a tabela mensal na parte do ano do pagamento; na Justiça Federal, essa parte tem retenção de 3%.") { Largura = 260 };
    private readonly CampoParcelasRraViewModel _parcelas = new();
    private readonly CampoTextoViewModel _correcao = Moeda("Correção monetária", "Atualização tributável paga sobre as parcelas, quando não está somada nelas. É rateada pelo valor das parcelas.");
    private readonly CampoTextoViewModel _juros = Moeda("Juros de mora (não tributáveis)", "Juros pelo atraso no pagamento de remuneração do trabalho (IN RFB 1.500/2014, art. 36, § 4º).");
    private readonly CampoTextoViewModel _despesas = Moeda("Despesas com a ação", "Custas e honorários de advogado pagos por você, sem reembolso. São rateadas pelo total recebido; a parte dos juros não é dedutível.");
    private readonly CampoTextoViewModel _inssAnteriores = Moeda("INSS dos anos anteriores", "Contribuição previdenciária oficial descontada sobre as parcelas de anos anteriores.");
    private readonly CampoTextoViewModel _pensaoAnteriores = Moeda("Pensão dos anos anteriores", "Pensão alimentícia judicial ou por escritura pública descontada sobre as parcelas de anos anteriores.");
    private readonly CampoTextoViewModel _inssAno = Moeda("INSS do ano do pagamento", "Contribuição descontada sobre as parcelas do ano do pagamento.");
    private readonly CampoTextoViewModel _pensaoAno = Moeda("Pensão do ano do pagamento", "Pensão alimentícia judicial descontada sobre as parcelas do ano do pagamento.");
    private readonly CampoTextoViewModel _totalParcelado = new("Total parcelado (opcional)", TipoCampo.Moeda, "0,00",
        "Só se o mesmo RRA é pago em meses diferentes: soma de todas as parcelas de anos anteriores, para dividir os meses pelo valor (art. 45, I). Deixe 0,00 se este é o único pagamento.");
    private readonly CampoOpcaoViewModel _reducao = new("Redução de 2026 (Lei 15.270)",
        [new("Aplicar, proporcional aos meses", true), new("Não aplicar", false)],
        "A IN RFB 1.500/2014, art. 37, manda observar a tabela de redução no RRA, sem detalhar a multiplicação pelos meses. Confira o critério da fonte pagadora.") { Largura = 260 };
    private readonly CampoTextoViewModel _outrosRendimentos = Moeda("Rendimentos normais do mês", "Salário ou benefício tributável pago pela mesma fonte no mês do pagamento, somado à parte do ano do pagamento.");
    private readonly CampoTextoViewModel _inssOutros = Moeda("INSS dos rendimentos normais", "Contribuição do mês sobre os rendimentos normais.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes do IRRF mensal. Não se aplicam à parte de anos anteriores.");

    public CalculadoraRra(ISimularDemonstrativoUseCase<SimularRraRequest> simulador)
    {
        _simulador = simulador;
        _origem.AoAlterar = () =>
        {
            var fontePagadora = _origem.Valor<OrigemPagamentoRra>() == OrigemPagamentoRra.FontePagadora;
            _outrosRendimentos.Visivel = _inssOutros.Visivel = _dependentes.Visivel = fontePagadora;
        };
        _origem.AoAlterar();
    }

    public override string Titulo => "IR sobre rendimentos recebidos acumuladamente (RRA)";
    public override string Descricao => "Estime o IRRF de pagamentos atrasados, separando os anos anteriores, tributados pela tabela acumulada, do ano do pagamento.";
    public override string InstrucaoInicial => "Informe a data do pagamento, gere os meses a que ele se refere, ajuste os valores e as deduções e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_dataPagamento, _origem, _reducao, _parcelas, _correcao, _juros, _despesas,
        _inssAnteriores, _pensaoAnteriores, _inssAno, _pensaoAno, _totalParcelado, _outrosRendimentos, _inssOutros, _dependentes];
    public override string NomeArquivoPdf => $"rra-{_dataPagamento.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Dictionary<string, string> ExportarCampos()
    {
        var campos = base.ExportarCampos();
        campos[_parcelas.Rotulo] = _parcelas.Exportar();
        return campos;
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        base.ImportarCampos(valores);
        if (valores.TryGetValue(_parcelas.Rotulo, out var json))
            _parcelas.Importar(json);
    }

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var parcelas = _parcelas.Ler();
        if (parcelas.Falhou)
            return parcelas.Erro;
        var fontePagadora = _origem.Valor<OrigemPagamentoRra>() == OrigemPagamentoRra.FontePagadora;
        return await LerECalcularAsync(_simulador, leitor =>
        {
            var totalParcelado = leitor.Moeda(_totalParcelado);
            // Campos ocultos na Justiça Federal não participam do cálculo, mesmo que tenham valores de antes da troca.
            return new SimularRraRequest(new PagamentoRra(leitor.Data(_dataPagamento), _origem.Valor<OrigemPagamentoRra>(), parcelas.Valor,
                leitor.Moeda(_correcao), leitor.Moeda(_juros), leitor.Moeda(_despesas), leitor.Moeda(_inssAnteriores), leitor.Moeda(_pensaoAnteriores),
                leitor.Moeda(_inssAno), leitor.Moeda(_pensaoAno), totalParcelado > 0m ? totalParcelado : null, _reducao.Valor<bool>(),
                fontePagadora ? leitor.Moeda(_outrosRendimentos) : 0m, fontePagadora ? leitor.Moeda(_inssOutros) : 0m,
                fontePagadora ? leitor.Inteiro(_dependentes) : 0));
        }, cancellationToken);
    }
}
