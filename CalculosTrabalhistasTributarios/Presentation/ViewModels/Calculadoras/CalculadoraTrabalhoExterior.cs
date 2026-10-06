using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraTrabalhoExterior : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularTrabalhoExteriorRequest> _simulador;
    private readonly IConsultaCotacoesTrabalhoExterior _cotacoes;
    private readonly CampoTextoViewModel _recebimento = new("Recebimento", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "Data em que o rendimento foi recebido; define o mês do carnê-leão.");
    private readonly CampoOpcaoViewModel _pais = new("País de origem", [
        new("Estados Unidos", "USD"), new("Portugal", "EUR"), new("Alemanha", "EUR"), new("Espanha", "EUR"),
        new("França", "EUR"), new("Itália", "EUR"), new("Países Baixos", "EUR"), new("Irlanda", "EUR"),
        new("Bélgica", "EUR"), new("Áustria", "EUR"), new("Finlândia", "EUR"), new("Grécia", "EUR"),
        new("Reino Unido", "GBP"), new("Canadá", "CAD"), new("Austrália", "AUD"), new("Suíça", "CHF"),
        new("Japão", "JPY"), new("Argentina", "ARS"), new("Chile", "CLP"), new("México", "MXN"),
        new("Colômbia", "COP"), new("Peru", "PEN"), new("Uruguai", "UYU"), new("Paraguai", "PYG"),
        new("China", "CNY"), new("Índia", "INR"), new("Coreia do Sul", "KRW"), new("Singapura", "SGD"),
        new("Nova Zelândia", "NZD"), new("África do Sul", "ZAR"), new("Outro país", "")
    ], "País da fonte pagadora. A moeda é sugerida; confira a moeda real do contrato e a elegibilidade para compensar o imposto.");
    private readonly CampoTextoViewModel _outroPais = new("Outro país (nome)", TipoCampo.Texto, "", "Preencha quando o país não estiver na lista.");
    private readonly CampoTextoViewModel _moeda = new("Moeda (ISO)", TipoCampo.Texto, "USD", "Código de três letras, como USD, EUR ou GBP. Para USD, informe 1 no campo USD por unidade.");
    private readonly CampoOpcaoViewModel _vinculo = new("Vínculo", [
        new("Emprego assalariado", VinculoTrabalhoExterior.EmpregoAssalariado),
        new("Serviço como PF", VinculoTrabalhoExterior.ServicoPessoaFisica)
    ], "Emprego com fonte estrangeira ou serviço prestado em nome próprio como pessoa física. Pessoa jurídica brasileira não é abrangida.");
    private readonly CampoTextoViewModel _remuneracao = Moeda("Remuneração (moeda)", "Valor bruto recebido na moeda estrangeira, antes de imposto, taxas e juros bancários.");
    private readonly CampoTextoViewModel _jurosRecebidos = Moeda("Juros recebidos (moeda)", "Juros de mora efetivamente recebidos pelo atraso do pagamento. Zero quando não houver.");
    private readonly CampoTextoViewModel _impostoExterior = Moeda("IR exterior retido (moeda)", "Imposto de renda sobre a remuneração tributável no Brasil, efetivamente retido no mesmo recebimento. Separe eventual imposto sobre juros de salário isentos; não inclua contribuições sociais.");
    private readonly CampoTextoViewModel _usdPorUnidade = new("USD por unidade", TipoCampo.Numero, "1", "Cotação oficial da moeda de origem em dólares dos EUA na data do recebimento. Para USD, use 1. Até seis casas decimais.");
    private readonly CampoTextoViewModel _dolarFiscal = new("Dólar compra fiscal", TipoCampo.Numero, "0", "R$/USD para compra publicado pelo Banco Central para o último dia útil da primeira quinzena do mês anterior ao recebimento.");
    private readonly CampoTextoViewModel _cambioEfetivo = new("Câmbio efetivo", TipoCampo.Numero, "0", "Reais recebidos por unidade da moeda estrangeira na operação bancária, antes das taxas informadas separadamente.");
    private readonly CampoTextoViewModel _taxaMoeda = Moeda("Taxa bancária (moeda)", "Taxa de transferência cobrada na moeda estrangeira, separada do spread já embutido no câmbio efetivo.");
    private readonly CampoTextoViewModel _jurosBanco = Moeda("Juros bancários (moeda)", "Juros efetivamente cobrados pela instituição sobre a operação. Não estime a partir de uma taxa sem o contrato.");
    private readonly CampoTextoViewModel _taxaReais = Moeda("Taxa bancária (R$)", "Taxa de recebimento ou transferência debitada em reais.");
    private readonly CampoOpcaoViewModel _creditoExterior = new("Compensar IR exterior", [
        new("Não confirmado", false), new("Elegibilidade confirmada", true)
    ], "Selecione confirmado apenas se houver tratado ou reciprocidade aplicável e o imposto não for restituído nem compensado no exterior.");
    private readonly CampoTextoViewModel _previdencia = Moeda("Previdência Brasil (R$)", "Contribuição à previdência oficial brasileira paga no mês e dedutível. Não informe previdência estrangeira.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Número de dependentes para o carnê-leão, quando adotadas deduções legais.");
    private readonly CampoTextoViewModel _pensao = Moeda("Pensão paga (R$)", "Pensão alimentícia dedutível paga no mês, quando adotadas deduções legais.");
    private readonly CampoTextoViewModel _livroCaixa = Moeda("Livro-caixa (R$)", "Apenas serviços como pessoa física: despesas necessárias, comprovadas e escrituradas, limitadas ao rendimento do mês.");
    private readonly CampoAtualizacaoCotacoesViewModel _atualizacao;

    public CalculadoraTrabalhoExterior(ISimularDemonstrativoUseCase<SimularTrabalhoExteriorRequest> simulador, IConsultaCotacoesTrabalhoExterior cotacoes)
    {
        _simulador = simulador;
        _cotacoes = cotacoes;
        _atualizacao = new CampoAtualizacaoCotacoesViewModel(new AsyncRelayCommand(AtualizarCotacoesAsync));
        _pais.AoAlterar = AtualizarPais;
        _vinculo.AoAlterar = AtualizarCampos;
        AtualizarPais();
        AtualizarCampos();
    }

    public override string Titulo => "Trabalho no exterior — residente no Brasil";
    public override string Descricao => "Simule salário ou serviços recebidos de fonte estrangeira como pessoa física residente fiscal no Brasil. " +
        "Atualize o dólar fiscal online e informe o câmbio efetivo e encargos do comprovante bancário.";
    public override string InstrucaoInicial => "Selecione o país, informe o recebimento e atualize as cotações. Confira a referência de outra moeda com a autoridade monetária de origem; informe o câmbio efetivo do comprovante.";
    public override IReadOnlyList<CampoViewModel> Campos => [_recebimento, _pais, _outroPais, _moeda, _vinculo, _remuneracao, _jurosRecebidos,
        _impostoExterior, _usdPorUnidade, _dolarFiscal, _cambioEfetivo, _atualizacao, _taxaMoeda, _jurosBanco, _taxaReais,
        _creditoExterior, _previdencia, _dependentes, _pensao, _livroCaixa];
    public override string NomeArquivoPdf => $"trabalho-exterior-{_recebimento.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    private void AtualizarCampos() => _livroCaixa.Visivel = _vinculo.Valor<VinculoTrabalhoExterior>() == VinculoTrabalhoExterior.ServicoPessoaFisica;

    private void AtualizarPais()
    {
        var moeda = _pais.Valor<string>();
        _outroPais.Visivel = moeda.Length == 0;
        if (moeda.Length > 0)
            _moeda.Valor = moeda;
        _usdPorUnidade.Valor = moeda == "USD" ? "1" : "0";
        _cambioEfetivo.Valor = "0";
        _atualizacao.Estado = "Atualize as cotações para a data do recebimento. O câmbio efetivo vem do comprovante bancário.";
    }

    public async Task AtualizarCotacoesAsync()
    {
        if (!DateOnly.TryParseExact(_recebimento.Valor.Trim(), "dd/MM/yyyy", Cultura, DateTimeStyles.None, out var recebimento))
        {
            _atualizacao.Estado = "Recebimento: informe uma data válida em DD/MM/AAAA antes de atualizar.";
            return;
        }
        _atualizacao.Estado = "Consultando Receita Federal e Banco Central...";
        var moeda = _moeda.Valor.Trim().ToUpperInvariant();
        var pais = _pais.Selecionada;
        var resultado = await _cotacoes.ConsultarAsync(recebimento, moeda, CancellationToken.None);
        if (_recebimento.Valor.Trim() != recebimento.ToString("dd/MM/yyyy", Cultura) ||
            _moeda.Valor.Trim().ToUpperInvariant() != moeda || _pais.Selecionada != pais)
        {
            _atualizacao.Estado = "Data, moeda ou país alterado durante a consulta. Atualize novamente para evitar cotações de outra operação.";
            return;
        }
        if (resultado.Falhou)
        {
            _atualizacao.Estado = resultado.Erro.Mensagem;
            return;
        }
        _dolarFiscal.Valor = resultado.Valor.DolarCompraFiscal.ToString("0.######", Cultura);
        if (resultado.Valor.DolaresPorUnidade is { } cruzada)
            _usdPorUnidade.Valor = cruzada.ToString("0.######", Cultura);
        else
            _usdPorUnidade.Valor = "0";
        _atualizacao.Estado = moeda == "USD"
            ? $"Receita: dólar compra fiscal de {recebimento:MM/yyyy} atualizado. USD por unidade = 1. Informe o câmbio efetivo do banco."
            : resultado.Valor.DolaresPorUnidade is not null
                ? $"Receita: dólar fiscal de {recebimento:MM/yyyy}; PTAX: USD/{moeda} em {recebimento:dd/MM/yyyy}. Confira a cotação da autoridade monetária do país de origem antes de calcular."
                : $"Receita: dólar fiscal de {recebimento:MM/yyyy}. PTAX de {moeda} indisponível nessa data; informe USD por unidade com fonte oficial.";
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        if (valores.TryGetValue(_pais.Rotulo, out var pais) && !_pais.Opcoes.Any(opcao => opcao.Texto == pais))
        {
            var adaptados = new Dictionary<string, string>(valores)
            {
                [_pais.Rotulo] = pais.Equals("EUA", StringComparison.OrdinalIgnoreCase) ? "Estados Unidos" : "Outro país"
            };
            if (adaptados[_pais.Rotulo] == "Outro país")
                adaptados.TryAdd(_outroPais.Rotulo, pais);
            valores = adaptados;
        }
        base.ImportarCampos(valores);
    }

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularTrabalhoExteriorRequest(
            leitor.Data(_recebimento), _outroPais.Visivel ? _outroPais.Valor : _pais.Selecionada.Texto, _moeda.Valor,
            new EntradaTrabalhoExterior(_vinculo.Valor<VinculoTrabalhoExterior>(), leitor.Moeda(_remuneracao),
                leitor.Moeda(_jurosRecebidos), leitor.Moeda(_impostoExterior), leitor.Moeda(_taxaMoeda),
                leitor.Moeda(_jurosBanco), leitor.Moeda(_taxaReais), leitor.Numero(_usdPorUnidade),
                leitor.Numero(_dolarFiscal), leitor.Numero(_cambioEfetivo)),
            _creditoExterior.Valor<bool>(), leitor.Moeda(_previdencia), leitor.Inteiro(_dependentes),
            leitor.Moeda(_pensao), _livroCaixa.Visivel ? leitor.Moeda(_livroCaixa) : 0m), cancellationToken);
}
