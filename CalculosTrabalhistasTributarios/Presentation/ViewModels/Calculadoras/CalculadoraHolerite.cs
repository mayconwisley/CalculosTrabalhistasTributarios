using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraHolerite : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularHoleriteRequest> _simulador;

    public CalculadoraHolerite(ISimularDemonstrativoUseCase<SimularHoleriteRequest> simulador)
    {
        _simulador = simulador;
        _trabalho = CamposNoturnos.Trabalho(_percentualNoturno);
        _diasComissoesManuais.AoAlterar = AtualizarCamposComissoes;
        AtualizarCamposComissoes();
    }

    private readonly CampoTextoViewModel _competencia = Competencia(dica: "Mês do holerite, que define os domingos para o DSR e as tabelas de INSS, IRRF e salário-família (MM/AAAA).");
    private readonly CampoTextoViewModel _salario = Moeda("Salário", "Salário-base mensal, sem adicionais. Deixe 0,00 para comissionista puro e informe as comissões no campo próprio.");
    private readonly CampoOpcaoViewModel _insalubridade = new("Insalubridade",
        [
            new("Não há", GrauInsalubridade.Nenhum),
            new("Grau mínimo (10%)", GrauInsalubridade.Minimo),
            new("Grau médio (20%)", GrauInsalubridade.Medio),
            new("Grau máximo (40%)", GrauInsalubridade.Maximo)
        ], "Percentual sobre o salário mínimo, conforme o laudo (NR-15). Não se acumula com a periculosidade.");
    private readonly CampoOpcaoViewModel _periculosidade = CampoOpcaoViewModel.SimNao("Periculosidade (30%)", false, "30% do salário (CLT, art. 193). Com insalubridade, vale o adicional maior.");
    private readonly CampoTextoViewModel _outrosProventos = Moeda("Proventos tributáveis", "Outros proventos com INSS, IRRF e FGTS. Se incluir comissões aqui, informe-as já com DSR e não as repita no campo próprio.");
    private readonly CampoTextoViewModel _comissoes = Moeda("Comissões do mês", "Comissões sem DSR; o repouso será calculado automaticamente, salvo se marcar que o valor já inclui DSR.");
    private readonly CampoOpcaoViewModel _comissoesIncluemDsr = CampoOpcaoViewModel.SimNao("Comissões já incluem DSR?", false, "Marque Sim se o valor das comissões informado já inclui o repouso remunerado.");
    private readonly CampoOpcaoViewModel _diasComissoesManuais = CampoOpcaoViewModel.SimNao("Informar dias das comissões?", false, "Use para escala ou período parcial. A contagem só altera o DSR das comissões, não o das horas extras.");
    private readonly CampoTextoViewModel _diasUteisComissoes = Inteiro("Dias úteis das comissões", 0, "Dias úteis do período de comissões; informe também os repousos previstos.");
    private readonly CampoTextoViewModel _diasDescansoComissoes = Inteiro("Repousos das comissões", 0, "Repousos e feriados previstos no período; os repousos perdidos são descontados pelo campo Descansos perdidos.");
    private readonly CampoTextoViewModel _pisoComissoes = Moeda("Garantia mínima das comissões", "Deixe 0,00 para o salário mínimo nacional no mês completo. Para dias informados, preencha a garantia do período; use o piso da categoria quando maior.");
    private readonly CampoTextoViewModel _premios = Moeda("Prêmios (só IRRF)", "Prêmios por desempenho superior ao esperado (CLT, art. 457, § 4º): têm IRRF, mas não têm INSS nem FGTS.");
    private readonly CampoTextoViewModel _proventosNaoTributaveis = Moeda("Proventos não tributáveis", "Ajuda de custo, diárias de viagem, reembolsos e outros valores sem INSS, IRRF e FGTS: só somam no líquido.");
    private readonly CampoTextoViewModel _divisor = new("Divisor de horas", TipoCampo.Numero, "220", "220 para 44 horas semanais; 200 para 40; 180 para 36; 150 para 30.");
    private readonly CampoTextoViewModel _horas1 = new("Horas extras (faixa 1)", TipoCampo.Horas, "0:00", "Horas extras com o primeiro adicional, como 10:30 ou 10,5.");
    private readonly CampoTextoViewModel _percentual1 = new("Adicional da faixa 1 (%)", TipoCampo.Numero, "50", "Mínimo de 50%; convenções coletivas podem prever mais.");
    private readonly CampoTextoViewModel _horas2 = new("Horas extras (faixa 2)", TipoCampo.Horas, "0:00", "Horas com o segundo adicional, como as de domingos e feriados.");
    private readonly CampoQuitacaoBancoHorasViewModel _quitacaoBancoHoras = new();
    private readonly CampoTextoViewModel _percentual2 = new("Adicional da faixa 2 (%)", TipoCampo.Numero, "100", "Normalmente 100% para domingos e feriados.");
    private readonly CampoTextoViewModel _horasNoturnas = new("Horas noturnas (relógio)", TipoCampo.Horas, "0:00", "Horas normais de relógio no período noturno e na prorrogação depois dele; no trabalho urbano, a conversão para a hora reduzida é automática. As horas extras noturnas vão no campo próprio.");
    private readonly CampoTextoViewModel _percentualNoturno = CamposNoturnos.Percentual();
    private readonly CampoOpcaoViewModel _trabalho;
    private readonly CampoTextoViewModel _horasExtrasNoturnas = new("Horas extras noturnas (relógio)", TipoCampo.Horas, "0:00", "Horas extras de relógio no período noturno, com o adicional da faixa 1 sobre a hora noturna (OJ 97 do TST); não as repita nas horas extras da faixa 1 nem nas horas noturnas.");
    private readonly CampoTextoViewModel _feriados = Inteiro("Feriados no mês", 0, "Feriados que caem em dias úteis, para o cálculo do DSR.");
    private readonly CampoTextoViewModel _faltas = Inteiro("Faltas (dias)", 0, "Faltas injustificadas, descontadas pelo salário-dia (salário e adicional ÷ 30).");
    private readonly CampoTextoViewModel _descansos = Inteiro("Descansos perdidos", 0, "Domingos e feriados perdidos pelas faltas: um por semana com falta injustificada, mais o feriado dessa semana (Lei 605/1949, art. 6º).");
    private readonly CampoTextoViewModel _atrasos = new("Atrasos (horas)", TipoCampo.Horas, "0:00", "Atrasos e saídas antecipadas do mês, descontados pelo valor da hora.");
    private readonly CampoTextoViewModel _intervaloIntra = new("Pausa suprimida (h)", TipoCampo.Horas, "0:00", "Horas:minutos de intervalo intrajornada para refeição ou descanso não concedido. Parcela separada, com acréscimo de 50%; não repita nas horas extras.");
    private readonly CampoTextoViewModel _intervaloInter = new("Interjornada (h)", TipoCampo.Horas, "0:00", "Horas:minutos faltantes para 11 horas entre jornadas. Parcela separada, com acréscimo de 50%; não repita nas horas extras.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes (IRRF)", 0, "Dependentes declarados para a dedução do IRRF, na modalidade de deduções legais. Os filhos do salário-família vão no campo próprio.");
    private readonly CampoTextoViewModel _filhos = Inteiro("Filhos (salário-família)", 0, "Filhos ou equiparados de até 14 anos, ou inválidos, para o salário-família.");
    private readonly CamposPensao _pensao = new("Pensão mensal definida na decisão ou no acordo.");
    private readonly CampoTextoViewModel _previdencia = Moeda("Previdência complementar", "Contribuição do trabalhador ao PGBL, ao fundo de pensão ou ao Fapi descontada no mês: deduzida por inteiro da base do IRRF nas deduções legais. A parte paga pela empresa não entra aqui.");
    private readonly CampoTextoViewModel _valeTransporte = Moeda("Custo do vale-transporte", "Custo das passagens do mês; o desconto é de até 6% do salário-base. Deixe 0,00 se não houver.");
    private readonly CampoTextoViewModel _adiantamento = Moeda("Adiantamento (vale)", "Adiantamento salarial pago no mês, descontado no holerite.");
    private readonly CampoTextoViewModel _outrosDescontos = Moeda("Descontos sem incidência", "Consignado, plano de saúde, mensalidades e outros descontos que saem do líquido sem reduzir o INSS, o IRRF nem o FGTS.");

    public override string Titulo => "Holerite do mês";
    public override string Descricao => "Monte o holerite com salário, adicionais, horas extras, faltas, vale-transporte, pensão e salário-família.";
    public override string InstrucaoInicial => "Informe o salário ou as comissões e os eventos do mês e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos =>
        [_competencia, _salario, _insalubridade, _periculosidade, _divisor, _horas1, _percentual1, _horas2, _percentual2, _quitacaoBancoHoras, _trabalho, _horasNoturnas, _percentualNoturno,
         _horasExtrasNoturnas, _feriados, _faltas, _descansos, _atrasos, _intervaloIntra, _intervaloInter, _comissoes, _comissoesIncluemDsr, _diasComissoesManuais,
         _diasUteisComissoes, _diasDescansoComissoes, _pisoComissoes, _outrosProventos, _premios, _proventosNaoTributaveis, _dependentes, .. _pensao.Campos, _previdencia, _filhos,
         _valeTransporte, _adiantamento, _outrosDescontos];
    public override string NomeArquivoPdf => $"holerite-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Dictionary<string, string> ExportarCampos()
    {
        var campos = base.ExportarCampos();
        campos[_quitacaoBancoHoras.Rotulo] = _quitacaoBancoHoras.Exportar();
        return campos;
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        base.ImportarCampos(valores);
        if (valores.TryGetValue(_quitacaoBancoHoras.Rotulo, out var json)) _quitacaoBancoHoras.Importar(json);
        else _quitacaoBancoHoras.DescartarCommand.Execute(null);
    }

    // Cálculos salvos no histórico antes da troca dos rótulos voltam nos campos novos.
    protected override IReadOnlyDictionary<string, string> RotulosAnteriores { get; } = new Dictionary<string, string>
    {
        ["Outros proventos"] = "Proventos tributáveis",
        ["Dependentes"] = "Dependentes (IRRF)",
        ["Outros descontos"] = "Descontos sem incidência"
    };

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var quitacao = _quitacaoBancoHoras.Ler();
        if (quitacao.Falhou) return quitacao.Erro;
        return await LerECalcularAsync(_simulador, leitor => new SimularHoleriteRequest(
            leitor.Competencia(_competencia), leitor.Moeda(_salario), _insalubridade.Valor<GrauInsalubridade>(), _periculosidade.Valor<bool>(), leitor.Moeda(_outrosProventos),
            leitor.Numero(_divisor), leitor.Horas(_horas1), leitor.Numero(_percentual1), leitor.Horas(_horas2), leitor.Numero(_percentual2), leitor.Horas(_horasNoturnas), leitor.Numero(_percentualNoturno),
            leitor.Inteiro(_feriados), leitor.Inteiro(_faltas), leitor.Inteiro(_descansos), leitor.Horas(_atrasos), leitor.Inteiro(_dependentes), leitor.Inteiro(_filhos), leitor.Pensao(_pensao),
            leitor.Moeda(_valeTransporte), leitor.Moeda(_adiantamento), leitor.Moeda(_outrosDescontos), _trabalho.Valor<bool>(), leitor.Horas(_horasExtrasNoturnas),
            leitor.Moeda(_premios), leitor.Moeda(_proventosNaoTributaveis), leitor.Moeda(_previdencia),
            leitor.Moeda(_comissoes), _comissoesIncluemDsr.Valor<bool>(),
            _diasComissoesManuais.Valor<bool>() ? leitor.Inteiro(_diasUteisComissoes) : null,
            _diasComissoesManuais.Valor<bool>() ? leitor.Inteiro(_diasDescansoComissoes) : null,
            leitor.Moeda(_pisoComissoes), leitor.Horas(_intervaloIntra), leitor.Horas(_intervaloInter), quitacao.Valor), cancellationToken);
    }

    private void AtualizarCamposComissoes()
    {
        var manuais = _diasComissoesManuais.Valor<bool>();
        _diasUteisComissoes.Visivel = _diasDescansoComissoes.Visivel = manuais;
    }
}
