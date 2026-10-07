using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraRescisao : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularRescisaoRequest> _simulador;
    private readonly CampoOpcaoViewModel _vinculo = new("Vínculo",
        [
            new("Empregado (CLT)", TipoVinculo.Empregado),
            new("Empregado doméstico", TipoVinculo.Domestico),
            new("Jovem aprendiz", TipoVinculo.Aprendiz)
        ], "O doméstico tem a indenização compensatória de 3,2% no lugar da multa de 40% do FGTS; o aprendiz tem FGTS de 2%.");
    private readonly CampoTextoViewModel _admissao = new("Data de admissão", TipoCampo.Data, DateTime.Today.AddYears(-2).ToString("dd/MM/yyyy", Cultura), "Data de início do contrato (dd/mm/aaaa).");
    private readonly CampoTextoViewModel _desligamento = new("Data de desligamento", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "Último dia de trabalho; com aviso trabalhado, o último dia do aviso (dd/mm/aaaa).");
    private readonly CampoOpcaoViewModel _motivo = new("Motivo",
        [
            new("Dispensa sem justa causa", MotivoRescisao.DispensaSemJustaCausa),
            new("Pedido de demissão", MotivoRescisao.PedidoDeDemissao),
            new("Acordo (art. 484-A)", MotivoRescisao.Acordo),
            new("Dispensa por justa causa", MotivoRescisao.DispensaPorJustaCausa),
            new("Fim de contrato a prazo", MotivoRescisao.TerminoDeContratoPorPrazo),
            new("Antecipada pela empresa", MotivoRescisao.RescisaoAntecipadaPeloEmpregador),
            new("Antecipada pelo empregado", MotivoRescisao.RescisaoAntecipadaPeloEmpregado)
        ], "Motivo do desligamento, que define as verbas devidas. As antecipadas encerram o contrato a prazo ou de experiência antes do fim previsto: pela empresa (CLT, art. 479) ou pelo empregado (art. 480).") { Largura = 300 };
    private readonly CampoTextoViewModel _fimPrevisto = new("Fim previsto do contrato", TipoCampo.Data, "", "Último dia previsto do contrato a prazo ou de experiência (dd/mm/aaaa).");
    private readonly CampoOpcaoViewModel _aviso = new("Aviso prévio",
        [
            new("Indenizado", CumprimentoAvisoPrevio.Indenizado),
            new("Trabalhado ou dispensado", CumprimentoAvisoPrevio.TrabalhadoOuDispensado),
            new("Não cumprido (descontar)", CumprimentoAvisoPrevio.NaoCumpridoPeloEmpregado)
        ], "Indenizado: pago sem trabalhar. Não cumprido: o empregado pediu demissão e não trabalhou o aviso.");
    private readonly CampoTextoViewModel _salario = Moeda("Salário", "Último salário mensal.");
    private readonly CampoTextoViewModel _medias = Moeda("Médias de variáveis", "Média de horas extras, comissões e adicionais, que entra no aviso, no 13º e nas férias.");
    private readonly CampoTextoViewModel _outrosProventos = Moeda("Outros proventos do mês", "Horas extras, adicionais e comissões do mês do desligamento, que têm INSS, IRRF e FGTS.");
    private readonly CampoQuitacaoBancoHorasViewModel _quitacaoBancoHoras = new();
    private readonly CampoTextoViewModel _verbasIndenizatorias = Moeda("Verbas indenizatórias", "Verbas da convenção ou do acordo sem INSS, IRRF e FGTS, como a multa normativa ou uma indenização por tempo de serviço.");
    private readonly CampoTextoViewModel _faltasNoMes = Inteiro("Faltas no mês", 0, "Faltas injustificadas no mês do desligamento, descontadas do saldo de salário.");
    private readonly CampoTextoViewModel _semanasComFalta = Inteiro("Semanas com falta no mês", 0, "Semanas do mês do desligamento com falta injustificada: cada uma perde o DSR, um dia de salário (Lei 605/1949, art. 6º).");
    private readonly CampoOpcaoViewModel _feriasVencidas = new("Férias vencidas",
        [new("Nenhuma", 0), new("1 período", 1), new("2 períodos", 2)], "Períodos aquisitivos completos cujas férias não foram tiradas.");
    private readonly CampoTextoViewModel _faltas = Inteiro("Faltas no período atual", 0, "Faltas injustificadas no período aquisitivo em curso, que reduzem as férias proporcionais.");
    private readonly CampoTextoViewModel _saldoFgts = Moeda("Saldo do FGTS", "Saldo do extrato para fins rescisórios; com 0,00, o histórico completo é usado. Sem histórico, a estimativa usa o salário atual.");
    private readonly CampoDepositosFgtsViewModel _depositosFgts;
    private readonly CampoTextoViewModel _adiantamento13 = Moeda("13º já adiantado", "1ª parcela do 13º paga neste ano, descontada na rescisão.");
    private readonly CampoTextoViewModel _outrosDescontos = Moeda("Outros descontos", "Vale-transporte, plano de saúde, vales e adiantamentos descontados na rescisão; no total, até uma remuneração mensal (CLT, art. 477, § 5º).");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução do IRRF.");
    private readonly CampoTextoViewModel _dataPagamento = new("Data do pagamento", TipoCampo.Data, "",
        "Deixe em branco se as verbas forem pagas no prazo, no mês do desligamento. Pagas no mês seguinte, o IRRF usa a tabela desse mês; depois de 10 dias do fim do contrato, há a multa de um salário (art. 477, § 8º).");
    private readonly CampoOpcaoViewModel _dataBase = new("Data-base da categoria",
        [new("Não informar", 0), .. Enumerable.Range(1, 12).Select(mes => new OpcaoCampo(Cultura.TextInfo.ToTitleCase(Cultura.DateTimeFormat.GetMonthName(mes)), mes))],
        "Mês do reajuste da categoria. Dispensado nos 30 dias antes dele, contando o aviso, o empregado recebe a indenização adicional de um salário (Lei 7.238/1984).");
    private readonly CamposPensao _pensao = new("Pensão definida na decisão ou no acordo, descontada do saldo de salário.");

    public CalculadoraRescisao(ISimularDemonstrativoUseCase<SimularRescisaoRequest> simulador)
    {
        _simulador = simulador;
        _depositosFgts = new(_admissao, _desligamento);
        _motivo.AoAlterar = AjustarAoMotivo;
        _vinculo.AoAlterar = AjustarAoMotivo;
        AjustarAoMotivo();
    }

    public override string Titulo => "Rescisão";
    public override string Descricao => "Calcule as verbas rescisórias conforme o motivo do desligamento, com aviso prévio proporcional, FGTS e multa.";
    public override string InstrucaoInicial => "Informe as datas, o motivo, o aviso prévio e o salário e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_vinculo, _admissao, _desligamento, _motivo, _aviso, _fimPrevisto, _salario, _medias, _outrosProventos, _quitacaoBancoHoras, _verbasIndenizatorias, _faltasNoMes, _semanasComFalta,
        _feriasVencidas, _faltas, _saldoFgts, _depositosFgts, _adiantamento13, _outrosDescontos, _dependentes, _dataPagamento, _dataBase, .. _pensao.Campos];
    public override string NomeArquivoPdf => $"rescisao-{_desligamento.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoSalario => _salario;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Dictionary<string, string> ExportarCampos()
    {
        var campos = base.ExportarCampos();
        campos[_depositosFgts.Rotulo] = _depositosFgts.Exportar();
        campos[_quitacaoBancoHoras.Rotulo] = _quitacaoBancoHoras.Exportar();
        return campos;
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        base.ImportarCampos(valores);
        if (valores.TryGetValue(_depositosFgts.Rotulo, out var json)) _depositosFgts.Importar(json);
        if (valores.TryGetValue(_quitacaoBancoHoras.Rotulo, out var quitacaoJson)) _quitacaoBancoHoras.Importar(quitacaoJson);
        else _quitacaoBancoHoras.DescartarCommand.Execute(null);
    }

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var historico = _depositosFgts.Visivel ? _depositosFgts.Ler() : Result.Ok<IReadOnlyList<DepositoFgtsHistorico>>([]);
        if (historico.Falhou) return historico.Erro;
        var quitacao = _quitacaoBancoHoras.Ler();
        if (quitacao.Falhou) return quitacao.Erro;
        var dataBase = _dataBase.Visivel ? _dataBase.Valor<int>() : 0;
        return await LerECalcularAsync(_simulador, leitor => new SimularRescisaoRequest(
            leitor.Data(_admissao), leitor.Data(_desligamento), _motivo.Valor<MotivoRescisao>(), _aviso.Valor<CumprimentoAvisoPrevio>(),
            leitor.Moeda(_salario), leitor.Moeda(_medias), _feriasVencidas.Valor<int>(), leitor.Inteiro(_faltas),
            _saldoFgts.Visivel ? leitor.Moeda(_saldoFgts) : 0m, leitor.Moeda(_adiantamento13), leitor.Inteiro(_dependentes), leitor.Pensao(_pensao),
            leitor.DataOpcional(_dataPagamento), _fimPrevisto.Visivel ? leitor.DataOpcional(_fimPrevisto) : null, dataBase == 0 ? null : dataBase,
            leitor.Moeda(_outrosProventos), leitor.Inteiro(_faltasNoMes), _vinculo.Valor<TipoVinculo>(), leitor.Inteiro(_semanasComFalta),
            leitor.Moeda(_outrosDescontos), leitor.Moeda(_verbasIndenizatorias), historico.Valor, quitacao.Valor), cancellationToken);
    }

    // O aviso só existe na dispensa sem justa causa, no pedido de demissão e no acordo; o saldo do FGTS só importa quando há
    // multa ou saque, e sempre no doméstico, porque estima a indenização compensatória; o fim previsto, nas rescisões
    // antecipadas; a data-base, na dispensa sem justa causa de quem tem categoria com data-base, o que não é o caso do doméstico.
    private void AjustarAoMotivo()
    {
        var motivo = _motivo.Valor<MotivoRescisao>();
        var domestico = _vinculo.Valor<TipoVinculo>() == TipoVinculo.Domestico;
        _aviso.Visivel = motivo is MotivoRescisao.DispensaSemJustaCausa or MotivoRescisao.PedidoDeDemissao or MotivoRescisao.Acordo;
        _saldoFgts.Visivel = domestico || motivo is MotivoRescisao.DispensaSemJustaCausa or MotivoRescisao.Acordo or MotivoRescisao.TerminoDeContratoPorPrazo or MotivoRescisao.RescisaoAntecipadaPeloEmpregador;
        _depositosFgts.Visivel = _saldoFgts.Visivel;
        _fimPrevisto.Visivel = motivo is MotivoRescisao.RescisaoAntecipadaPeloEmpregador or MotivoRescisao.RescisaoAntecipadaPeloEmpregado;
        _dataBase.Visivel = motivo == MotivoRescisao.DispensaSemJustaCausa && !domestico;
        var aviso = _aviso.Valor<CumprimentoAvisoPrevio>();
        if (motivo == MotivoRescisao.PedidoDeDemissao && aviso == CumprimentoAvisoPrevio.Indenizado)
            _aviso.Selecionada = _aviso.Opcoes[1];
        else if (motivo != MotivoRescisao.PedidoDeDemissao && aviso == CumprimentoAvisoPrevio.NaoCumpridoPeloEmpregado)
            _aviso.Selecionada = _aviso.Opcoes[0];
    }
}
