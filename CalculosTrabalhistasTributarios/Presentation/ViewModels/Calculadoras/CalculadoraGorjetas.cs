using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraGorjetas : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularGorjetasRequest> _simulador;
    private readonly CampoOpcaoViewModel _regime = new("Regime da empresa",
        [new("Simples Nacional (até 20%)", true), new("Demais regimes (até 33%)", false)],
        "Limite da retenção para os encargos: até 20% da arrecadação no Simples Nacional e até 33% nos demais regimes (CLT, art. 457, § 6º).") { Largura = 250 };
    private readonly CampoTextoViewModel _retencao = new("Retenção da empresa (%)", TipoCampo.Numero, "0",
        "Percentual retido para os encargos, só com previsão em convenção ou acordo coletivo; 0 sem retenção.");
    private readonly CampoOpcaoViewModel _situacao = new("Situação do contrato",
        [new("Contrato ativo", false), new("Dispensa sem justa causa", true)],
        "Na dispensa sem justa causa entra a multa de 40% do FGTS, e as férias ainda não gozadas são indenizadas, sem FGTS. As gorjetas não entram no aviso prévio (Súmula 354 do TST).") { Largura = 250 };
    private readonly CampoTextoViewModel _admissao = new("Admissão (opcional)", TipoCampo.Data, string.Empty,
        "Data de admissão (dd/mm/aaaa): separa as férias por período aquisitivo. Sem ela, todos os meses formam um só período.", permiteVazio: true);
    private readonly CampoGorjetasViewModel _meses = new();

    public CalculadoraGorjetas(ISimularDemonstrativoUseCase<SimularGorjetasRequest> simulador) => _simulador = simulador;

    public override string Titulo => "Gorjetas";
    public override string Descricao => "Calcule as gorjetas repassadas por competência, a retenção da empresa, a média de 12 meses e os reflexos no 13º, nas férias e no FGTS.";
    public override string InstrucaoInicial => "Escolha o regime e a retenção, gere os meses, informe a parte do empregado nas gorjetas de cada mês e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_regime, _retencao, _situacao, _admissao, _meses];
    public override string NomeArquivoPdf => "gorjetas.pdf";

    public override Dictionary<string, string> ExportarCampos()
    {
        var campos = base.ExportarCampos();
        campos[_meses.Rotulo] = _meses.Exportar();
        return campos;
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        base.ImportarCampos(valores);
        if (valores.TryGetValue(_meses.Rotulo, out var json))
            _meses.Importar(json);
    }

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var meses = _meses.Ler();
        if (meses.Falhou)
            return meses.Erro;
        var dispensa = _situacao.Valor<bool>();
        var incidencias = IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias | IncidenciasReflexo.Fgts
            | (dispensa ? IncidenciasReflexo.MultaFgts : IncidenciasReflexo.Nenhuma);
        return await LerECalcularAsync(_simulador, leitor => new SimularGorjetasRequest(new EntradaGorjetas(
            meses.Valor, _regime.Valor<bool>(), leitor.Numero(_retencao), incidencias, FeriasGozadas: !dispensa, Admissao: leitor.DataOpcional(_admissao))), cancellationToken);
    }
}
