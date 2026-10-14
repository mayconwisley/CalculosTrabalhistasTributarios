using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Transferencia;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraTransferencia : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularTransferenciaRequest> _simulador;
    private readonly CampoTextoViewModel _percentual = new("Percentual do adicional (%)", TipoCampo.Numero, "25",
        "No mínimo 25% do salário da origem (CLT, art. 469, § 3º); informe mais se a norma coletiva ou o contrato previr.");
    private readonly CampoOpcaoViewModel _situacao = new("Situação do contrato",
        [new("Contrato ativo", false), new("Dispensa sem justa causa", true)],
        "Na dispensa durante a transferência entram o reflexo no aviso prévio indenizado e a multa de 40% do FGTS; as férias ainda não gozadas são indenizadas, sem FGTS (OJ 195 do TST).") { Largura = 250 };
    private readonly CampoTextoViewModel _admissao = new("Admissão (opcional)", TipoCampo.Data, string.Empty,
        "Data de admissão (dd/mm/aaaa): separa as férias por período aquisitivo. Sem ela, todos os meses formam um só período.", permiteVazio: true);
    private readonly CampoTextoViewModel _diasAviso = Inteiro("Dias de aviso prévio", 30, "De 30 a 90 dias: 30 mais 3 por ano completo de serviço (Lei 12.506/2011).");
    private readonly CampoTextoViewModel _despesas = Moeda("Despesas (opcional)",
        "Despesas da mudança pagas pelo empregador, como transporte e frete (CLT, art. 470): reembolso, sem FGTS nem reflexos. Deixe 0,00 se não houver.");
    private readonly CampoTransferenciaViewModel _meses = new();

    public CalculadoraTransferencia(ISimularDemonstrativoUseCase<SimularTransferenciaRequest> simulador)
    {
        _simulador = simulador;
        _situacao.AoAlterar = () => _diasAviso.Visivel = _situacao.Valor<bool>();
        _situacao.AoAlterar();
    }

    public override string Titulo => "Adicional de transferência";
    public override string Descricao => "Calcule o adicional de no mínimo 25% da transferência provisória por competência, com 13º, férias, aviso e FGTS.";
    public override string InstrucaoInicial => "Informe o percentual e a situação do contrato, gere os meses da transferência, ajuste salário e dias e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_percentual, _situacao, _admissao, _diasAviso, _despesas, _meses];
    public override string NomeArquivoPdf => "adicional-transferencia.pdf";

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
            | (dispensa ? IncidenciasReflexo.AvisoPrevio | IncidenciasReflexo.MultaFgts : IncidenciasReflexo.Nenhuma);
        return await LerECalcularAsync(_simulador, leitor => new SimularTransferenciaRequest(new EntradaTransferencia(
            meses.Valor, leitor.Numero(_percentual), incidencias, leitor.Moeda(_despesas),
            dispensa ? leitor.Inteiro(_diasAviso) : 0, FeriasGozadas: !dispensa, Admissao: leitor.DataOpcional(_admissao))), cancellationToken);
    }
}
