using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraIrpfAnual(ISimularDemonstrativoUseCase<SimularIrpfAnualRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _ano = Inteiro("Ano-calendário", Math.Max(2026, DateTime.Today.Year), "Ano dos rendimentos; a declaração é entregue no ano seguinte. Disponível desde 2026, com a Lei 15.270/2025.");
    private readonly CampoTextoViewModel _tributaveis = Moeda("Rendimentos tributáveis", "Salários, pró-labore, aluguéis e outros rendimentos tributáveis no ajuste do ano, sem o 13º e a PLR.");
    private readonly CampoTextoViewModel _previdencia = Moeda("Previdência oficial", "INSS ou regime próprio descontado no ano.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "R$ 2.275,08 por dependente no modelo completo.");
    private readonly CampoTextoViewModel _medicas = Moeda("Despesas médicas", "Dedutíveis sem limite no modelo completo, descontados os reembolsos.");
    private readonly CampoTextoViewModel _instrucao = Moeda("Instrução", "Até R$ 3.561,50 por pessoa (titular e dependentes) no modelo completo.");
    private readonly CampoTextoViewModel _pgbl = Moeda("Previdência privada (PGBL)", "PGBL e FAPI, até 12% dos rendimentos tributáveis, para quem contribui para a previdência oficial.");
    private readonly CampoTextoViewModel _pensao = Moeda("Pensão alimentícia paga", "Pensão judicial, de acordo homologado ou de escritura pública, dedutível no modelo completo.");
    private readonly CampoTextoViewModel _impostoPago = Moeda("Imposto retido ou pago", "IRRF retido sobre os rendimentos tributáveis, carnê-leão e pagamentos complementares do ano.");
    private readonly CampoTextoViewModel _dividendos = Moeda("Dividendos recebidos", "Lucros e dividendos recebidos no ano, que entram na tributação mínima das altas rendas.");
    private readonly CampoTextoViewModel _irrfDividendos = Moeda("IRRF sobre dividendos", "Retenção de 10% sobre os dividendos acima de R$ 50 mil no mês, compensada na tributação mínima.");
    private readonly CampoTextoViewModel _outros = Moeda("Outros rendimentos", "Isentos ou de tributação exclusiva que entram na tributação mínima: 13º, PLR, JCP, aplicações financeiras. Não inclua poupança, LCI, LCA, heranças nem indenizações.");
    private readonly CampoTextoViewModel _impostoExclusivo = Moeda("Imposto exclusivo pago", "Imposto exclusivo ou definitivo pago sobre os outros rendimentos, como o do 13º, da PLR e dos JCP.");
    private readonly CampoTextoViewModel _aliquotaEmpresa = new("Alíquota da empresa (%)", TipoCampo.Numero, "0", "Alíquota efetiva de IRPJ e CSLL da empresa que pagou os dividendos, para o redutor da tributação mínima; 0 para não calcular.");
    private readonly CampoOpcaoViewModel _tipoEmpresa = new("Tipo da empresa",
        [
            new("Em geral (34%)", AliquotaNominalEmpresa.Geral),
            new("Seguradora ou financeira (40%)", AliquotaNominalEmpresa.Financeira),
            new("Banco (45%)", AliquotaNominalEmpresa.Banco)
        ], "Alíquota nominal de IRPJ e CSLL que limita a soma das alíquotas efetivas da empresa e da pessoa (Lei 9.250/1995, art. 16-B).");

    public override string Titulo => "IRPF anual";
    public override string Descricao => "Declaração do ano com a redução anual, os modelos completo e simplificado e a tributação mínima das altas rendas (Lei 15.270/2025).";
    public override string InstrucaoInicial => "Informe os rendimentos, as deduções e o imposto já pago no ano e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos =>
        [_ano, _tributaveis, _previdencia, _dependentes, _medicas, _instrucao, _pgbl, _pensao, _impostoPago, _dividendos, _irrfDividendos, _outros, _impostoExclusivo, _aliquotaEmpresa, _tipoEmpresa];
    public override string NomeArquivoPdf => $"irpf-{_ano.Valor.Trim()}.pdf";

    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularIrpfAnualRequest(
            leitor.Inteiro(_ano), leitor.Moeda(_tributaveis), leitor.Moeda(_previdencia), leitor.Inteiro(_dependentes), leitor.Moeda(_medicas), leitor.Moeda(_instrucao), leitor.Moeda(_pgbl), leitor.Moeda(_pensao),
            leitor.Moeda(_impostoPago), leitor.Moeda(_dividendos), leitor.Moeda(_irrfDividendos), leitor.Moeda(_outros), leitor.Moeda(_impostoExclusivo), leitor.Numero(_aliquotaEmpresa),
            _tipoEmpresa.Valor<AliquotaNominalEmpresa>()), cancellationToken);
}
