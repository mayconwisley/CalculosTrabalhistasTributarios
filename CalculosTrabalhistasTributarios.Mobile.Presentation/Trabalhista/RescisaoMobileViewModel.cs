using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;
using CalculosTrabalhistasTributarios.Presentation;
using System.Globalization;
using System.Windows.Input;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class RescisaoMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularRescisaoRequest> _simulador;
    private readonly RelayCommand _adicionarDeposito;
    private string? _avisoHistorico;
    public override bool ExibirHistoricoFgts => Campo("saldoFgts").Visivel;
    public override bool TemDepositosFgts => DepositosFgts.Count > 0;
    public override string? AvisoHistorico => _avisoHistorico;
    public override ICommand AdicionarDepositoCommand => _adicionarDeposito;
    public override ICommand RemoverDepositoCommand { get; }
    public override ICommand GerarDepositosCommand { get; }
    public override string Titulo => "Rescisão";
    public override string Descricao => "Verbas do desligamento conforme motivo, aviso, férias, 13º e FGTS. Saldo de FGTS zero gera estimativa pelo salário atual.";

    public RescisaoMobileViewModel(ISimularDemonstrativoUseCase<SimularRescisaoRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<RescisaoMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("vinculo", "Vínculo", TipoCampoMobile.Opcao, opcoes: ["Empregado (CLT)", "Empregado doméstico", "Jovem aprendiz"]);
        Adicionar("admissao", "Data de admissão (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.AddYears(-2).ToString("dd/MM/yyyy", Cultura));
        Adicionar("desligamento", "Data de desligamento (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura));
        Adicionar("motivo", "Motivo do desligamento", TipoCampoMobile.Opcao, opcoes: ["Dispensa sem justa causa", "Pedido de demissão", "Acordo (art. 484-A)", "Dispensa por justa causa", "Fim de contrato a prazo", "Antecipada pela empresa", "Antecipada pelo empregado"]);
        Adicionar("aviso", "Aviso prévio", TipoCampoMobile.Opcao, opcoes: ["Indenizado", "Trabalhado ou dispensado", "Não cumprido pelo empregado"]);
        Adicionar("fimPrevisto", "Fim previsto do contrato (dd/MM/yyyy)", TipoCampoMobile.Data, dica: "Informe nas rescisões antecipadas do contrato a prazo.");
        Adicionar("salario", "Último salário mensal (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("medias", "Médias de variáveis (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("outrosProventos", "Outros proventos do mês (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("indenizatorias", "Verbas indenizatórias (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("faltasMes", "Faltas no mês", TipoCampoMobile.Inteiro, "0");
        Adicionar("semanasFalta", "Semanas com falta no mês", TipoCampoMobile.Inteiro, "0");
        Adicionar("feriasVencidas", "Férias vencidas", TipoCampoMobile.Opcao, opcoes: ["Nenhuma", "1 período", "2 períodos"]);
        Adicionar("faltasPeriodo", "Faltas no período aquisitivo atual", TipoCampoMobile.Inteiro, "0");
        Adicionar("saldoFgts", "Saldo do FGTS (R$)", TipoCampoMobile.Moeda, "0,00", "Use o saldo para fins rescisórios do extrato. Zero produz uma estimativa.");
        Adicionar("adiantamento13", "13º já adiantado (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("outrosDescontos", "Outros descontos (R$)", TipoCampoMobile.Moeda, "0,00");
        Adicionar("dependentes", "Dependentes para IRRF", TipoCampoMobile.Inteiro, "0");
        Adicionar("pagamento", "Data do pagamento (dd/MM/yyyy, opcional)", TipoCampoMobile.Data, dica: "Em branco considera pagamento no prazo, no mês do desligamento.");
        Adicionar("dataBase", "Mês da data-base da categoria", TipoCampoMobile.Opcao,
            opcoes: ["Não informar", .. Enumerable.Range(1, 12).Select(m => Cultura.TextInfo.ToTitleCase(Cultura.DateTimeFormat.GetMonthName(m)))]);
        AdicionarPensao();
        _adicionarDeposito = new RelayCommand(AdicionarLinha, () => DepositosFgts.Count < 720);
        RemoverDepositoCommand = new RelayCommand<LinhaDepositoFgtsMobile>(linha => { if (linha is not null) DepositosFgts.Remove(linha); });
        GerarDepositosCommand = new RelayCommand(GerarMeses);
        DepositosFgts.CollectionChanged += (_, _) =>
        {
            Demonstrativo = null;
            _adicionarDeposito.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(TemDepositosFgts));
        };
        Atualizar();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave is "motivo" or "vinculo" or "pensaoBase") Atualizar();
    }

    private void Atualizar()
    {
        var motivo = Campo("motivo").Indice;
        var domestico = Campo("vinculo").Indice == 1;
        Campo("aviso").Visivel = motivo is 0 or 1 or 2;
        Campo("fimPrevisto").Visivel = motivo is 5 or 6;
        Campo("saldoFgts").Visivel = domestico || motivo is 0 or 2 or 4 or 5;
        OnPropertyChanged(nameof(ExibirHistoricoFgts));
        Campo("dataBase").Visivel = motivo == 0 && !domestico;
        if (motivo == 1 && Campo("aviso").Indice == 0) Campo("aviso").Indice = 1;
        else if (motivo != 1 && Campo("aviso").Indice == 2) Campo("aviso").Indice = 0;
        AtualizarPensao();
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var historico = LerHistorico();
        var motivo = (MotivoRescisao)Opcao("motivo");
        var aviso = Campo("aviso").Visivel ? (CumprimentoAvisoPrevio)Opcao("aviso") : CumprimentoAvisoPrevio.TrabalhadoOuDispensado;
        var dataBase = Campo("dataBase").Visivel ? Opcao("dataBase") : 0;
        return _simulador.ExecutarAsync(new SimularRescisaoRequest(Data("admissao"), Data("desligamento"), motivo, aviso,
            Numero("salario"), Numero("medias"), Opcao("feriasVencidas"), Inteiro("faltasPeriodo"),
            Campo("saldoFgts").Visivel ? Numero("saldoFgts") : 0m, Numero("adiantamento13"), Inteiro("dependentes"),
            Pensao(), DataOpcional("pagamento"), Campo("fimPrevisto").Visivel ? DataOpcional("fimPrevisto") : null,
            dataBase == 0 ? null : dataBase, Numero("outrosProventos"), Inteiro("faltasMes"), (TipoVinculo)Opcao("vinculo"),
            Inteiro("semanasFalta"), Numero("outrosDescontos"), Numero("indenizatorias"), historico), cancellationToken);
    }

    private void AdicionarLinha()
    {
        if (DepositosFgts.Count >= 720) return;
        var linha = new LinhaDepositoFgtsMobile();
        linha.PropertyChanged += (_, _) => Demonstrativo = null;
        DepositosFgts.Add(linha);
    }

    private void GerarMeses()
    {
        DateOnly admissao;
        DateOnly desligamento;
        try { admissao = Data("admissao"); desligamento = Data("desligamento"); }
        catch (EntradaInvalidaException erro) { Mensagem = erro.Message; return; }
        if (desligamento < admissao) { Mensagem = "Desligamento: informe data igual ou posterior à admissão."; return; }
        var quantidade = (desligamento.Year - admissao.Year) * 12 + desligamento.Month - admissao.Month;
        if (quantidade > 720) { Mensagem = "O histórico aceita até 720 competências; informe o saldo do extrato."; return; }
        var existentes = DepositosFgts.GroupBy(linha => linha.Competencia).ToDictionary(grupo => grupo.Key, grupo => grupo.First().Valor);
        DepositosFgts.Clear();
        var primeiro = new DateOnly(admissao.Year, admissao.Month, 1);
        for (var i = 0; i < quantidade; i++)
        {
            AdicionarLinha();
            var competencia = primeiro.AddMonths(i).ToString("MM/yyyy", Cultura);
            DepositosFgts[^1].Competencia = competencia;
            DepositosFgts[^1].Valor = existentes.GetValueOrDefault(competencia) ?? "0,00";
        }
        Mensagem = null;
        _avisoHistorico = $"{quantidade} competência(s) gerada(s); os valores existentes nesses meses foram preservados.";
        OnPropertyChanged(nameof(AvisoHistorico));
    }

    private IReadOnlyList<DepositoFgtsHistorico> LerHistorico()
    {
        if (!ExibirHistoricoFgts) return [];
        if (DepositosFgts.Count > 720) throw new EntradaInvalidaException("O histórico do FGTS aceita até 720 competências.");
        var valores = new List<DepositoFgtsHistorico>(DepositosFgts.Count);
        foreach (var linha in DepositosFgts)
        {
            if (!DateOnly.TryParseExact("01/" + linha.Competencia.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var competencia))
                throw new EntradaInvalidaException($"FGTS {linha.Competencia}: informe a competência em MM/yyyy.");
            if (!LeituraNumerica.TentarLer(linha.Valor, out var valor))
                throw new EntradaInvalidaException($"FGTS {linha.Competencia}: informe o depósito em reais, por exemplo 250,00.");
            valores.Add(new(competencia, valor));
        }
        return valores;
    }
}
