using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RegraResgate = CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar.CalculadoraResgatePrevidencia;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class ResgatePrevidenciaMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private readonly ISimularDemonstrativoUseCase<SimularResgatePrevidenciaRequest> _simulador;
    private readonly GradeMobile _aportes;
    private bool _confirmarSubstituicao;

    public override string Titulo => "Resgate de previdência privada";
    public override string Descricao => "Calcule o IR no resgate de PGBL ou VGBL pelo prazo de cada aporte, nos regimes regressivo e progressivo, e o valor líquido.";

    public ResgatePrevidenciaMobileViewModel(ISimularDemonstrativoUseCase<SimularResgatePrevidenciaRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<ResgatePrevidenciaMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("plano", "Plano e resgate");
        Adicionar("tipo", "Tipo de plano", TipoCampoMobile.Opcao,
            dica: "PGBL: o IR incide sobre todo o valor resgatado. VGBL: só sobre os rendimentos.", opcoes: ["PGBL", "VGBL"]);
        Adicionar("regime", "Regime de tributação", TipoCampoMobile.Opcao,
            dica: "Regressivo: IR definitivo pelo prazo de cada aporte. Progressivo: 15% retidos e ajuste na declaração. Desde a Lei 14.803/2024, a escolha pode ser feita até o primeiro resgate.",
            opcoes: ["Regressivo (35% a 10%)", "Progressivo (15% retidos)"]);
        Adicionar("dataResgate", "Data do resgate (dd/mm/aaaa)", TipoCampoMobile.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
            "Define o prazo de cada aporte na tabela regressiva.");
        Adicionar("resgate", "Resgate", TipoCampoMobile.Opcao,
            dica: "Total: todo o saldo. Parcial: o valor informado, consumindo primeiro os aportes mais antigos.", opcoes: ["Total", "Parcial"]);
        Adicionar("valor", "Valor do resgate parcial (R$)", TipoCampoMobile.Moeda, "0,00",
            "Valor bruto a resgatar, antes do IR; no máximo o saldo total dos aportes.");

        Secao("gerador", "Gerar aportes mensais (opcional)");
        Adicionar("primeiroAporte", "Primeiro aporte (dd/mm/aaaa)", TipoCampoMobile.Data, DateTime.Today.AddYears(-3).ToString("dd/MM/yyyy", Cultura),
            "Os aportes seguintes são mensais, a partir desta data.");
        Adicionar("quantidade", "Quantidade de aportes", TipoCampoMobile.Inteiro, "12", $"De 1 a {RegraResgate.LimiteAportes}, sem passar da data do resgate.");
        Adicionar("valorAporte", "Valor por aporte (R$)", TipoCampoMobile.Moeda, "0,00", "Também sugerido nas linhas acrescentadas com + Aporte.");
        Adicionar("rentabilidade", "Rentabilidade (% ao mês)", TipoCampoMobile.Numero, "0,00",
            "Entre -10% e 10%, como 0,60. Estima o saldo de cada aporte até o resgate; prefira os saldos do extrato.");

        _aportes = AdicionarGrade("aportes", "Aportes do plano",
            "Data, valor aportado e saldo atual de cada aporte, conforme o extrato. Gerar aportes substitui a grade após confirmação.",
            "Aporte", RegraResgate.LimiteAportes,
            [
                new("data", "Data (dd/mm/aaaa)", TipoCampoMobile.Data),
                new("aportado", "Valor aportado (R$)", TipoCampoMobile.Moeda, "0,00"),
                new("saldo", "Saldo atual (R$)", TipoCampoMobile.Moeda, "0,00", "Saldo do aporte no extrato, na data do resgate.")
            ], SugerirValores);
        _aportes.GerarCommand = new RelayCommand(GerarAportes);
        _aportes.RotuloGerar = "Gerar aportes";
        // Qualquer mudança na grade exige nova confirmação antes de substituí-la.
        _aportes.Alterada += (_, _) => _confirmarSubstituicao = false;
        AtualizarVisibilidade();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "resgate") AtualizarVisibilidade();
    }

    private void AtualizarVisibilidade() => Campo("valor").Visivel = Campo("resgate").Indice == 1;

    // Linha nova acrescentada pelo usuário: aportado e saldo partem do valor por aporte, como no desktop.
    private void SugerirValores(LinhaGradeMobile linha)
    {
        if (_aportes.Linhas.Contains(linha)) return;
        var aportado = linha.Campo("aportado");
        var saldo = linha.Campo("saldo");
        if (aportado.Valor != "0,00" || saldo.Valor != "0,00") return;
        try
        {
            var valor = Numero("valorAporte");
            if (valor <= 0m) return;
            aportado.Valor = saldo.Valor = valor.ToString("N2", Cultura);
        }
        catch (EntradaInvalidaException) { }
    }

    private void GerarAportes()
    {
        try
        {
            var primeiro = Data("primeiroAporte");
            var resgate = Data("dataResgate");
            var quantidade = Inteiro("quantidade");
            if (quantidade is < 1 or > RegraResgate.LimiteAportes)
                throw new EntradaInvalidaException($"Informe de 1 a {RegraResgate.LimiteAportes} aportes mensais.");
            var valor = Numero("valorAporte");
            var rentabilidade = Numero("rentabilidade");
            if (valor <= 0m || rentabilidade is < -10m or > 10m)
                throw new EntradaInvalidaException("Informe o valor por aporte, como 500,00, e a rentabilidade ao mês entre -10% e 10%, como 0,60.");
            if (primeiro.AddMonths(quantidade - 1) > resgate)
                throw new EntradaInvalidaException("Os aportes gerados passariam da data do resgate: reduza a quantidade ou antecipe o primeiro aporte.");
            if (_aportes.Linhas.Count > 0 && !_confirmarSubstituicao)
            {
                _aportes.Aviso = $"Os {_aportes.Linhas.Count} aportes da grade serão substituídos. Toque em Gerar aportes novamente para confirmar.";
                _confirmarSubstituicao = true;
                return;
            }

            var aportado = decimal.Round(valor, 2);
            while (_aportes.Linhas.Count > 0) _aportes.Linhas.RemoveAt(_aportes.Linhas.Count - 1);
            for (var indice = 0; indice < quantidade; indice++)
            {
                var data = primeiro.AddMonths(indice);
                _aportes.AdicionarLinha(new Dictionary<string, string>
                {
                    ["data"] = data.ToString("dd/MM/yyyy", Cultura),
                    ["aportado"] = aportado.ToString("N2", Cultura),
                    ["saldo"] = RegraResgate.EstimarSaldo(aportado, data, resgate, rentabilidade).ToString("N2", Cultura)
                });
            }
            _confirmarSubstituicao = false;
            _aportes.Aviso = $"{quantidade} aporte(s) de {primeiro.ToString("dd/MM/yyyy", Cultura)} a {primeiro.AddMonths(quantidade - 1).ToString("dd/MM/yyyy", Cultura)}, "
                + $"com saldo estimado a {rentabilidade.ToString("0.##", Cultura)}% ao mês. Substitua pelos saldos do extrato, se os tiver.";
        }
        catch (EntradaInvalidaException erro) { _aportes.Aviso = erro.Message; }
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        if (_aportes.Linhas.Count == 0)
            throw new EntradaInvalidaException("Gere os aportes ou adicione os aportes do plano.");
        var aportes = _aportes.Ler(linha => new AportePrevidencia(LeituraCampoMobile.Data(linha.Campo("data")),
            LeituraCampoMobile.Numero(linha.Campo("aportado")), LeituraCampoMobile.Numero(linha.Campo("saldo"))));
        var tipo = Opcao("tipo") == 0 ? TipoPlanoPrevidencia.Pgbl : TipoPlanoPrevidencia.Vgbl;
        var regime = Opcao("regime") == 0 ? RegimeTributacaoPrevidencia.Regressivo : RegimeTributacaoPrevidencia.Progressivo;
        var parcial = Opcao("resgate") == 1;
        return _simulador.ExecutarAsync(new SimularResgatePrevidenciaRequest(new EntradaResgatePrevidencia(
            tipo, regime, aportes, Data("dataResgate"), parcial ? Numero("valor") : null)), cancellationToken);
    }
}
