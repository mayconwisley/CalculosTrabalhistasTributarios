using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;
using CalculosTrabalhistasTributarios.Domain.Pensao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>
/// Pensão alimentícia em atraso: gera as parcelas do período, recebe os pagamentos parciais e calcula a correção, os juros e
/// a separação entre o rito da prisão e o da penhora.
/// </summary>
public sealed class PensaoAtrasoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private const int MaximoParcelas = 240;
    private static readonly HashSet<string> CamposDasParcelas = ["base", "valorPensao", "percentual", "diaVencimento", "primeiraParcela", "ultimaParcela"];
    private readonly ISimularPensaoAtrasoUseCase _simulador;
    private readonly ILogger<PensaoAtrasoMobileViewModel> _logger;
    // As parcelas da grade foram geradas com outro valor, percentual, dia ou período e serão geradas de novo ao calcular.
    private bool _parcelasDesatualizadas;

    public override string Titulo => "Pensão em atraso";
    public override string Descricao => "Atualize parcelas atrasadas com correção e juros e separe os ritos da prisão e da penhora.";

    public PensaoAtrasoMobileViewModel(ISimularPensaoAtrasoUseCase simulador, ICompartilhamentoPdf compartilhamento,
        ILogger<PensaoAtrasoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        _logger = logger;
        // Por padrão, os 12 meses até o anterior ao atual, que já venceram.
        var ultima = DateTime.Today.AddMonths(-1);
        Secao("secaoParcelas", "Pensão e período");
        Adicionar("base", "Valor da pensão", TipoCampoMobile.Opcao, dica: "Valor fixo por mês ou percentual do salário mínimo de cada mês.",
            opcoes: ["Valor fixo", "% do salário mínimo"]);
        Adicionar("valorPensao", "Valor mensal (R$)", TipoCampoMobile.Moeda, "0,00", "Valor de cada parcela na decisão ou no acordo.");
        Adicionar("percentual", "Percentual do salário mínimo (%)", TipoCampoMobile.Numero, "0,00",
            "Aplicado ao salário mínimo de cada mês; por exemplo, 30.");
        Adicionar("diaVencimento", "Dia do vencimento", TipoCampoMobile.Inteiro, "10", "De 1 a 31; nos meses mais curtos, vence no último dia.");
        Adicionar("primeiraParcela", "Primeira parcela (mm/aaaa)", TipoCampoMobile.Competencia, ultima.AddMonths(-11).ToString("MM/yyyy", Cultura));
        Adicionar("ultimaParcela", "Última parcela (mm/aaaa)", TipoCampoMobile.Competencia, ultima.ToString("MM/yyyy", Cultura),
            "Até 240 parcelas (20 anos).");
        Secao("secaoAtualizacao", "Atualização e cobrança");
        Adicionar("dataCalculo", "Data do cálculo (dd/mm/aaaa)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Correção e juros vão até esta data; nenhuma parcela pode vencer depois dela.");
        Adicionar("dataAjuizamento", "Ajuizamento (dd/mm/aaaa, opcional)", TipoCampoMobile.Data, "",
            "Data em que a execução foi proposta. Vazio: a separação dos ritos considera a data do cálculo.");
        Adicionar("correcao", "Correção monetária", TipoCampoMobile.Opcao, opcoes: ["INPC", "IPCA", "Sem correção"]);
        Adicionar("juros", "Juros de mora", TipoCampoMobile.Opcao,
            opcoes: [DemonstrativoPensaoAtraso.NomeJuros(JurosDeMora.UmPorCentoAteTaxaLegal), DemonstrativoPensaoAtraso.NomeJuros(JurosDeMora.UmPorCento),
                DemonstrativoPensaoAtraso.NomeJuros(JurosDeMora.TaxaLegal), DemonstrativoPensaoAtraso.NomeJuros(JurosDeMora.Nenhum)]);
        Adicionar("acrescimos", "Multa e honorários (rito da penhora)", TipoCampoMobile.Opcao,
            dica: "Do art. 523 do CPC, sobre o débito do rito da penhora não pago em 15 dias da intimação.",
            opcoes: ["Não incluir", "Multa de 10% e honorários de 10%", "Só a multa de 10%"]);

        var parcelas = AdicionarGrade("parcelas", "Parcelas",
            "Toque em Gerar parcelas e informe os pagamentos parciais. Ao gerar de novo, as parcelas do período são refeitas e os valores pagos continuam nos mesmos meses.",
            "Parcela", MaximoParcelas,
            [
                new("mes", "Mês (mm/aaaa)", TipoCampoMobile.Competencia),
                new("vencimento", "Vencimento (dd/mm/aaaa)", TipoCampoMobile.Data),
                new("devido", "Devido (R$)", TipoCampoMobile.Moeda, "0,00"),
                new("pago", "Pago (R$)", TipoCampoMobile.Moeda, "0,00", "Pagamento parcial abatido no vencimento; 0,00 se não houve.")
            ]);
        var gerar = new AsyncRelayCommand(GerarParcelasAsync, () => !Ocupado);
        parcelas.GerarCommand = gerar;
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Ocupado)) gerar.NotifyCanExecuteChanged();
        };
        parcelas.RotuloGerar = "Gerar parcelas";
        AtualizarBase();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "base") AtualizarBase();
        if (CamposDasParcelas.Contains(campo.Chave) && Grades.Count > 0 && Grade("parcelas").TemLinhas)
            _parcelasDesatualizadas = true;
    }

    private void AtualizarBase()
    {
        var valorFixo = Campo("base").Indice == 0;
        Campo("valorPensao").Visivel = valorFixo;
        Campo("percentual").Visivel = !valorFixo;
    }

    private async Task GerarParcelasAsync(CancellationToken cancellationToken)
    {
        Mensagem = null;
        try
        {
            Ocupado = true;
            var resultado = await PreencherParcelasAsync(cancellationToken);
            if (resultado.Falhou) Mensagem = resultado.Erro.Mensagem;
        }
        catch (EntradaInvalidaException erro) { Mensagem = erro.Message; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception erro)
        {
            _logger.LogError(erro, "Falha ao gerar as parcelas da pensão em atraso.");
            Mensagem = "Não foi possível gerar as parcelas. Confira os dados e tente novamente.";
        }
        finally { Ocupado = false; }
    }

    /// <summary>
    /// Refaz as parcelas do período pelo caso de uso, como o botão Gerar parcelas do desktop: o valor devido e o vencimento
    /// vêm da geração, e os pagamentos parciais já informados continuam nos mesmos meses.
    /// </summary>
    private async Task<Result> PreencherParcelasAsync(CancellationToken cancellationToken)
    {
        var valorFixo = Opcao("base") == 0;
        var pedido = new GerarParcelasAtrasoRequest(valorFixo ? BasePensao.ValorFixo : BasePensao.SalarioMinimo,
            valorFixo ? Numero("valorPensao") : 0m, valorFixo ? 0m : Numero("percentual"),
            Competencia("primeiraParcela"), Competencia("ultimaParcela"), Inteiro("diaVencimento"));
        var geradas = await _simulador.GerarParcelasAsync(pedido, cancellationToken);
        if (geradas.Falhou) return geradas.Erro;
        cancellationToken.ThrowIfCancellationRequested();

        var grade = Grade("parcelas");
        var pagos = grade.Linhas.GroupBy(linha => linha.Campo("mes").Valor.Trim())
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Last().Campo("pago").Valor);
        grade.Remocao.Descartar();
        grade.Linhas.Clear();
        foreach (var parcela in geradas.Valor)
        {
            var mes = parcela.Competencia.ToString("MM/yyyy", Cultura);
            var pago = pagos.TryGetValue(mes, out var informado) ? informado : parcela.Pago.ToString("N2", Cultura);
            grade.AdicionarLinha(new Dictionary<string, string>
            {
                ["mes"] = mes,
                ["vencimento"] = parcela.Vencimento.ToString("dd/MM/yyyy", Cultura),
                ["devido"] = parcela.Devido.ToString("N2", Cultura),
                ["pago"] = pago
            });
        }
        _parcelasDesatualizadas = false;
        grade.Aviso = pagos.Count > 0
            ? $"{grade.Linhas.Count} parcela(s) gerada(s); os valores pagos já informados continuam nos mesmos meses."
            : $"{grade.Linhas.Count} parcela(s) gerada(s).";
        return Result.Ok();
    }

    protected override async Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        // Sem parcelas, ou com parcelas de outro valor ou período, o cálculo parte das parcelas do período informado.
        var grade = Grade("parcelas");
        if (!grade.TemLinhas || _parcelasDesatualizadas)
        {
            var geracao = await PreencherParcelasAsync(cancellationToken);
            if (geracao.Falhou) return geracao.Erro;
        }
        var dataCalculo = Data("dataCalculo");
        var ajuizamento = DataOpcional("dataAjuizamento");
        var parcelas = grade.Ler(linha => new ParcelaPensaoInformada(
            LeituraCampoMobile.Competencia(linha.Campo("mes")), LeituraCampoMobile.Data(linha.Campo("vencimento")),
            LeituraCampoMobile.Numero(linha.Campo("devido")), LeituraCampoMobile.Numero(linha.Campo("pago"))));

        var resultado = await _simulador.CalcularAsync(new SimularPensaoAtrasoRequest(parcelas, dataCalculo, ajuizamento,
            (CorrecaoMonetaria)Opcao("correcao"), (JurosDeMora)Opcao("juros"), (AcrescimosPenhora)Opcao("acrescimos")), cancellationToken);
        return resultado.Map(simulacao => DemonstrativoPensaoAtraso.Montar(simulacao));
    }
}
