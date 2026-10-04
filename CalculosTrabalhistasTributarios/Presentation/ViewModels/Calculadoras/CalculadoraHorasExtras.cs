using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraHorasExtras : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularHorasExtrasRequest> _simulador;

    public CalculadoraHorasExtras(ISimularDemonstrativoUseCase<SimularHorasExtrasRequest> simulador)
    {
        _simulador = simulador;
        _trabalho = CamposNoturnos.Trabalho(_percentualNoturno);
    }

    private readonly CampoTextoViewModel _competencia = Competencia(dica: "Mês das horas, que define os domingos para o DSR e as tabelas de INSS e IRRF (MM/AAAA).");
    private readonly CampoTextoViewModel _salario = Moeda("Salário", "Salário-base mensal.");
    private readonly CampoTextoViewModel _adicionais = Moeda("Adicionais salariais", "Insalubridade, periculosidade e outros adicionais fixos do mês, que entram no valor da hora (Súmula 264 do TST).");
    private readonly CampoTextoViewModel _divisor = new("Divisor de horas", TipoCampo.Numero, "220", "220 para 44 horas semanais; 200 para 40; 180 para 36; 150 para 30.");
    private readonly CampoTextoViewModel _horas1 = new("Horas extras (faixa 1)", TipoCampo.Horas, "0:00", "Horas extras com o primeiro adicional, como 10:30 ou 10,5.");
    private readonly CampoTextoViewModel _percentual1 = new("Adicional da faixa 1 (%)", TipoCampo.Numero, "50", "Mínimo de 50%; convenções coletivas podem prever mais.");
    private readonly CampoTextoViewModel _horas2 = new("Horas extras (faixa 2)", TipoCampo.Horas, "0:00", "Horas com o segundo adicional, como as de domingos e feriados.");
    private readonly CampoTextoViewModel _percentual2 = new("Adicional da faixa 2 (%)", TipoCampo.Numero, "100", "Normalmente 100% para domingos e feriados.");
    private readonly CampoTextoViewModel _horasNoturnas = new("Horas noturnas (relógio)", TipoCampo.Horas, "0:00", "Horas normais de relógio entre 22h e 5h, e na prorrogação depois das 5h; a conversão para a hora reduzida é automática. As horas extras noturnas vão no campo próprio.");
    private readonly CampoTextoViewModel _horasExtrasNoturnas = new("Horas extras noturnas (relógio)", TipoCampo.Horas, "0:00", "Horas extras de relógio no período noturno, com o adicional da faixa 1 sobre a hora noturna (OJ 97 do TST); não as repita nas horas extras da faixa 1 nem nas horas noturnas.");
    private readonly CampoTextoViewModel _percentualNoturno = CamposNoturnos.Percentual();
    private readonly CampoOpcaoViewModel _trabalho;
    private readonly CampoTextoViewModel _feriados = Inteiro("Feriados no mês", 0, "Feriados que caem em dias úteis, para o cálculo do DSR.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução do IRRF.");

    public override string Titulo => "Horas extras e adicionais";
    public override string Descricao => "Calcule horas extras, adicional noturno e o reflexo no DSR, com o salário líquido do mês.";
    public override string InstrucaoInicial => "Informe o salário, o divisor e as horas do mês e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _salario, _adicionais, _divisor, _horas1, _percentual1, _horas2, _percentual2, _trabalho, _horasNoturnas, _percentualNoturno, _horasExtrasNoturnas, _feriados, _dependentes];
    public override string NomeArquivoPdf => $"horas-extras-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularHorasExtrasRequest(
            leitor.Competencia(_competencia), leitor.Moeda(_salario), leitor.Numero(_divisor),
            leitor.Horas(_horas1), leitor.Numero(_percentual1), leitor.Horas(_horas2), leitor.Numero(_percentual2),
            leitor.Horas(_horasNoturnas), leitor.Numero(_percentualNoturno), leitor.Inteiro(_feriados), leitor.Inteiro(_dependentes),
            leitor.Moeda(_adicionais), leitor.Horas(_horasExtrasNoturnas), _trabalho.Valor<bool>()), cancellationToken);
}
