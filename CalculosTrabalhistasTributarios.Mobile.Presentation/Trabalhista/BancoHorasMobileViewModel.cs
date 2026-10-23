using System.Globalization;
using System.Windows.Input;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Inicio;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class BancoHorasMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private const int LimiteLancamentos = 500;
    private readonly ISimularDemonstrativoUseCase<SimularBancoHorasRequest> _simulador;
    private readonly INavegacaoCalculadoras _navegacao;
    private readonly RelayCommand _adicionarLancamento;

    public override string Titulo => "Banco de horas";
    public override string Descricao => "Concilie créditos e folgas de um ciclo e estime a quitação do saldo positivo no fechamento ou na rescisão.";
    public override bool ExibirLancamentosBancoHoras => true;
    public override bool TemLancamentosBancoHoras => LancamentosBancoHoras.Count > 0;
    public override ICommand AdicionarLancamentoCommand => _adicionarLancamento;
    public override ICommand RemoverLancamentoCommand { get; }
    public override ICommand UsarNoHoleriteCommand { get; }
    public override ICommand UsarNaRescisaoCommand { get; }

    public BancoHorasMobileViewModel(ISimularDemonstrativoUseCase<SimularBancoHorasRequest> simulador, INavegacaoCalculadoras navegacao,
        ICompartilhamentoPdf compartilhamento, ILogger<BancoHorasMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        _navegacao = navegacao;
        Adicionar("regime", "Regime de compensação", TipoCampoMobile.Opcao,
            dica: "Define o prazo máximo do ciclo: o mesmo mês, até 6 meses por acordo individual escrito ou até 1 ano por norma coletiva.",
            opcoes: ["Mesmo mês", "Acordo individual escrito (até 6 meses)", "Acordo ou convenção coletiva (até 1 ano)"]);
        Adicionar("situacao", "Situação", TipoCampoMobile.Opcao,
            dica: "Acompanhamento mostra só o saldo. Fechamento e rescisão estimam o pagamento das horas positivas restantes.",
            opcoes: ["Acompanhamento", "Fechamento do ciclo", "Rescisão"]);
        Adicionar("inicio", "Início do ciclo (dd/MM/yyyy)", TipoCampoMobile.Data,
            new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("dd/MM/yyyy", Cultura));
        Adicionar("fim", "Fim do ciclo (dd/MM/yyyy)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "No fechamento, define a competência do holerite; na rescisão, a data de desligamento.");
        Adicionar("salario", "Salário mensal (R$)", TipoCampoMobile.Moeda, "0,00",
            "Opcional no acompanhamento; obrigatório para estimar a quitação.");
        Adicionar("divisor", "Divisor de horas", TipoCampoMobile.Numero, "220", "220 para 44 h/semana; 200 para 40; 180 para 36.");
        Adicionar("adicional", "Adicional padrão (%)", TipoCampoMobile.Numero, "50",
            "Entre 50% e 1.000%. Cada crédito pode ter um adicional próprio.");
        _adicionarLancamento = new RelayCommand(AdicionarLinha, () => LancamentosBancoHoras.Count < LimiteLancamentos);
        RemoverLancamentoCommand = new RelayCommand<LinhaLancamentoBancoHorasMobile>(linha =>
        {
            if (linha is not null) LancamentosBancoHoras.Remove(linha);
        });
        UsarNoHoleriteCommand = new AsyncRelayCommand(() => TransferirAsync(TipoCalculadoraMobile.Holerite, SituacaoBancoHoras.Fechamento));
        UsarNaRescisaoCommand = new AsyncRelayCommand(() => TransferirAsync(TipoCalculadoraMobile.Rescisao, SituacaoBancoHoras.Rescisao));
        LancamentosBancoHoras.CollectionChanged += (_, args) =>
        {
            if (args.OldItems is not null)
                foreach (LinhaLancamentoBancoHorasMobile linha in args.OldItems) linha.PropertyChanged -= LinhaAlterada;
            if (args.NewItems is not null)
                foreach (LinhaLancamentoBancoHorasMobile linha in args.NewItems) linha.PropertyChanged += LinhaAlterada;
            Demonstrativo = null;
            Mensagem = null;
            _adicionarLancamento.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(TemLancamentosBancoHoras));
        };
        LancamentosBancoHoras.Add(new LinhaLancamentoBancoHorasMobile { Data = DateTime.Today.ToString("dd/MM/yyyy", Cultura) });
    }

    private void AdicionarLinha()
    {
        if (LancamentosBancoHoras.Count >= LimiteLancamentos) return;
        var linha = new LinhaLancamentoBancoHorasMobile { Data = Texto("fim") };
        LancamentosBancoHoras.Add(linha);
        PaginaLancamentosBancoHoras.Exibir(linha);
    }

    private void LinhaAlterada(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        Demonstrativo = null;
        Mensagem = null;
    }

    private async Task TransferirAsync(TipoCalculadoraMobile destino, SituacaoBancoHoras situacao)
    {
        if (Demonstrativo?.QuitacaoBancoHoras is { } quitacao && quitacao.Situacao == situacao)
            await _navegacao.AbrirAsync(destino, quitacao);
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken) =>
        _simulador.ExecutarAsync(new SimularBancoHorasRequest(Data("inicio"), Data("fim"), (RegimeBancoHoras)Opcao("regime"),
            (SituacaoBancoHoras)Opcao("situacao"), Numero("salario"), Numero("divisor"), Numero("adicional"), LerLancamentos()), cancellationToken);

    private IReadOnlyList<LancamentoBancoHoras> LerLancamentos()
    {
        if (LancamentosBancoHoras.Count > LimiteLancamentos)
            throw new EntradaInvalidaException($"O ciclo aceita até {LimiteLancamentos} lançamentos.");
        var lancamentos = new List<LancamentoBancoHoras>(LancamentosBancoHoras.Count);
        LinhaLancamentoBancoHorasMobile? atual = null;
        try
        {
            for (var i = 0; i < LancamentosBancoHoras.Count; i++)
            {
                var linha = atual = LancamentosBancoHoras[i];
                var nome = $"Lançamento {i + 1}";
                if (!DateOnly.TryParseExact(linha.Data.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var data))
                    throw new EntradaInvalidaException($"{nome}: informe a data em dd/MM/yyyy.");
                if (linha.Tipo is < 0 or > 1)
                    throw new EntradaInvalidaException($"{nome}: selecione crédito ou compensação.");
                if (!TentarMinutos(linha.Horas, out var minutos))
                    throw new EntradaInvalidaException($"{nome}: informe as horas de relógio como 1:30, até 10:00.");
                decimal? adicional = null;
                if (linha.EhCredito && !string.IsNullOrWhiteSpace(linha.Adicional))
                {
                    if (!LeituraNumerica.TentarLer(linha.Adicional, out var percentual))
                        throw new EntradaInvalidaException($"{nome}: informe o adicional em percentual ou deixe vazio para usar o padrão.");
                    adicional = percentual;
                }
                lancamentos.Add(new(data, (TipoLancamentoBancoHoras)linha.Tipo, minutos, linha.Descricao.Trim(), adicional));
            }
        }
        catch (EntradaInvalidaException)
        {
            // Mostra a página do lançamento citado na mensagem.
            if (atual is not null) PaginaLancamentosBancoHoras.Exibir(atual);
            throw;
        }
        return lancamentos;
    }

    // O banco de horas guarda minutos inteiros: aceita somente horas de relógio, para que 1:30 não vire 1,30 hora.
    private static bool TentarMinutos(string texto, out int minutos)
    {
        minutos = 0;
        var partes = texto.Trim().Split(':');
        if (partes.Length != 2 || !int.TryParse(partes[0], NumberStyles.None, Cultura, out var horas)
            || !int.TryParse(partes[1], NumberStyles.None, Cultura, out var resto) || horas > 10 || resto > 59)
            return false;
        minutos = horas * 60 + resto;
        return true;
    }
}
