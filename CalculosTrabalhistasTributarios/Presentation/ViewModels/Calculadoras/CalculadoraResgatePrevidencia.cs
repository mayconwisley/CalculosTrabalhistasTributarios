using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia.Complementar;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraResgatePrevidencia : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularResgatePrevidenciaRequest> _simulador;
    private readonly CampoOpcaoViewModel _tipo = new("Tipo de plano",
        [new("PGBL", TipoPlanoPrevidencia.Pgbl), new("VGBL", TipoPlanoPrevidencia.Vgbl)],
        "PGBL: o IR incide sobre todo o valor resgatado. VGBL: só sobre os rendimentos.");
    private readonly CampoOpcaoViewModel _regime = new("Regime de tributação",
        [new("Regressivo (35% a 10%)", RegimeTributacaoPrevidencia.Regressivo), new("Progressivo (15% retidos)", RegimeTributacaoPrevidencia.Progressivo)],
        "Regressivo: IR definitivo pelo prazo de cada aporte. Progressivo: 15% retidos e ajuste na declaração. Desde a Lei 14.803/2024, a escolha pode ser feita até o primeiro resgate.") { Largura = 250 };
    private readonly CampoTextoViewModel _dataResgate = new("Data do resgate", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
        "Data do resgate (dd/mm/aaaa): define o prazo de cada aporte na tabela regressiva.");
    private readonly CampoOpcaoViewModel _resgate = new("Resgate",
        [new("Total", false), new("Parcial", true)],
        "Total: todo o saldo. Parcial: o valor informado, consumindo primeiro os aportes mais antigos.");
    private readonly CampoTextoViewModel _valor = Moeda("Valor do resgate parcial", "Valor bruto a resgatar, antes do IR; no máximo o saldo total dos aportes.");
    private readonly CampoAportesPrevidenciaViewModel _aportes;

    public CalculadoraResgatePrevidencia(ISimularDemonstrativoUseCase<SimularResgatePrevidenciaRequest> simulador)
    {
        _simulador = simulador;
        _aportes = new CampoAportesPrevidenciaViewModel(_dataResgate);
        _resgate.AoAlterar = () => _valor.Visivel = _resgate.Valor<bool>();
        _resgate.AoAlterar();
    }

    public override string Titulo => "Resgate de previdência privada";
    public override string Descricao => "Calcule o IR no resgate de PGBL ou VGBL pelo prazo de cada aporte, nos regimes regressivo e progressivo, e o valor líquido.";
    public override string InstrucaoInicial => "Escolha o plano e o regime, informe a data e o tipo de resgate, gere ou informe os aportes com o saldo de cada um e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_tipo, _regime, _dataResgate, _resgate, _valor, _aportes];
    public override string NomeArquivoPdf => "resgate-previdencia.pdf";

    public override Dictionary<string, string> ExportarCampos()
    {
        var campos = base.ExportarCampos();
        campos[_aportes.Rotulo] = _aportes.Exportar();
        return campos;
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        base.ImportarCampos(valores);
        if (valores.TryGetValue(_aportes.Rotulo, out var json))
            _aportes.Importar(json);
    }

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var aportes = _aportes.Ler();
        if (aportes.Falhou)
            return aportes.Erro;
        var parcial = _resgate.Valor<bool>();
        return await LerECalcularAsync(_simulador, leitor => new SimularResgatePrevidenciaRequest(new EntradaResgatePrevidencia(
            _tipo.Valor<TipoPlanoPrevidencia>(), _regime.Valor<RegimeTributacaoPrevidencia>(), aportes.Valor,
            leitor.Data(_dataResgate), parcial ? leitor.Moeda(_valor) : null)), cancellationToken);
    }
}
