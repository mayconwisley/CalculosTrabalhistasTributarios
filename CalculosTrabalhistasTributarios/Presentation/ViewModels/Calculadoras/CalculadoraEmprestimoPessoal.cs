using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Financeiro;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraEmprestimoPessoal : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularEmprestimoPessoalRequest> _simulador;
    private readonly CampoTextoViewModel _valor = Moeda("Valor solicitado (R$)", "Crédito antes dos descontos na liberação. Seguro, outros custos financiados e IOF financiado são somados automaticamente.");
    private readonly CampoTextoViewModel _parcelas = Inteiro("Parcelas (meses)", 24, "De 1 a 120 meses; primeira parcela um mês após a liberação, sem carência.");
    private readonly CampoTextoViewModel _juros = new("Juros (% ao mês)", TipoCampo.Numero, "0", "Taxa mensal da proposta, de 0 a 20%, até seis casas decimais. Não informe taxa anual nem CET.")
    { MostrarDica = true, DicaEmLinha = "Informe a taxa mensal da proposta; não use taxa anual nem CET." };
    private readonly CampoTextoViewModel _data = new("Data de liberação", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "De 01/01/2025 a 31/12/2100. Define o calendário mensal e os dias para estimar o IOF.");
    private readonly CampoOpcaoViewModel _modoIof = new("Cálculo do IOF", [new("Automático (estimado)", true), new("Informado pelo banco", false)]);
    private readonly CampoOpcaoViewModel _cobrancaIof = new("Cobrança do IOF", [new("Financiado", true), new("Descontado do crédito", false)], "Financiado aumenta o saldo devedor; descontado reduz o valor recebido.") { MostrarDica = true };
    private readonly CampoTextoViewModel _iof = Moeda("IOF informado (R$)", "IOF da proposta; informe zero apenas se o banco confirmou ausência de cobrança.");
    private readonly CampoTextoViewModel _seguro = Moeda("Seguro financiado (R$)", "Opcional: preço do seguro em reais, não o percentual de cobertura.");
    private readonly CampoTextoViewModel _custos = Moeda("Outros financiados (R$)", "Opcional: demais custos somados ao financiamento. Não repita seguro nem IOF.");
    private readonly CampoTextoViewModel _descontos = Moeda("Custos na liberação (R$)", "Opcional: custos retidos do solicitado, exceto IOF. Não repita valores financiados.");

    public CalculadoraEmprestimoPessoal(ISimularDemonstrativoUseCase<SimularEmprestimoPessoalRequest> simulador)
    {
        _simulador = simulador;
        _modoIof.AoAlterar = () => _iof.Visivel = !_modoIof.Valor<bool>();
        _modoIof.AoAlterar();
    }

    public override string Titulo => "Empréstimo pessoal";
    public override string Descricao => "Simule empréstimo pessoal PF com parcelas mensais Price, sem carência. A primeira parcela vence um mês após a liberação. IOF, seguro e demais custos são separados; não há margem consignável.";
    public override string InstrucaoInicial => "Informe valor, prazo, taxa mensal e liberação. IOF automático ou informado pelo banco, financiado ou descontado do crédito. Seguro e demais custos são opcionais: zero se não houver. Custo efetivo estimado; contratos com juros diários ou primeira parcela em outra data exigem cálculo específico.";
    public override IReadOnlyList<CampoViewModel> Campos => [_valor, _parcelas, _juros, _data, _modoIof, _cobrancaIof, _iof, _seguro, _custos, _descontos];
    public override string NomeArquivoPdf => "emprestimo-pessoal.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularEmprestimoPessoalRequest(
            new EntradaEmprestimoPessoal(leitor.Moeda(_valor), leitor.Inteiro(_parcelas), leitor.Numero(_juros),
                leitor.Data(_data), _modoIof.Valor<bool>(), _iof.Visivel ? leitor.Moeda(_iof) : 0m,
                _cobrancaIof.Valor<bool>(), leitor.Moeda(_seguro), leitor.Moeda(_custos), leitor.Moeda(_descontos))), cancellationToken);
}
