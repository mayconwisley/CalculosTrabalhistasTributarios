using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraSobreaviso : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularSobreavisoRequest> _simulador;
    private readonly CampoTextoViewModel _divisor = Inteiro("Divisor do salário-hora", 220, "Salário mensal ÷ divisor = salário-hora: 220 para 44 horas semanais, 200 para 40, 180 para 36.");
    private readonly CampoOpcaoViewModel _habitualidade = new("Pagamento das horas",
        [new("Habitual (com reflexos)", true), new("Eventual (só o FGTS)", false)],
        "Horas pagas com habitualidade integram a remuneração e refletem no DSR, no 13º, nas férias e no aviso; as eventuais têm só o FGTS do mês.") { Largura = 250 };
    private readonly CampoOpcaoViewModel _situacao = new("Situação do contrato",
        [new("Contrato ativo", false), new("Dispensa sem justa causa", true)],
        "Na dispensa sem justa causa entram o reflexo no aviso prévio indenizado e a multa de 40% do FGTS; as férias ainda não gozadas são indenizadas, sem FGTS (OJ 195 do TST).") { Largura = 250 };
    private readonly CampoOpcaoViewModel _dsr = CampoOpcaoViewModel.SimNao("DSR sobre as horas?", true,
        "Repouso semanal sobre as horas habituais: valor do mês ÷ dias úteis × domingos e feriados (Lei 605/1949, art. 7º).");
    private readonly CampoOpcaoViewModel _dsrNosReflexos = CampoOpcaoViewModel.SimNao("DSR entra nos reflexos?", false,
        "Premissa: se o DSR das horas entra na base do 13º, das férias e do aviso. O marco de 20/03/2023 do TST (Tema 9) trata das horas extras.");
    private readonly CampoTextoViewModel _admissao = new("Admissão (opcional)", TipoCampo.Data, string.Empty,
        "Data de admissão (dd/mm/aaaa): separa as férias por período aquisitivo. Sem ela, todos os meses formam um só período.", permiteVazio: true);
    private readonly CampoTextoViewModel _diasAviso = Inteiro("Dias de aviso prévio", 30, "De 30 a 90 dias: 30 mais 3 por ano completo de serviço (Lei 12.506/2011).");
    private readonly CampoSobreavisoViewModel _meses = new();

    public CalculadoraSobreaviso(ISimularDemonstrativoUseCase<SimularSobreavisoRequest> simulador)
    {
        _simulador = simulador;
        _habitualidade.AoAlterar = _situacao.AoAlterar = _dsr.AoAlterar = AtualizarCampos;
        AtualizarCampos();
    }

    public override string Titulo => "Sobreaviso e prontidão";
    public override string Descricao => "Calcule as horas de sobreaviso (1/3) e de prontidão (2/3) por competência, com DSR, 13º, férias, aviso e FGTS.";
    public override string InstrucaoInicial => "Escolha as premissas, gere os meses, informe o salário e as horas de cada competência e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_divisor, _habitualidade, _situacao, _admissao, _dsr, _dsrNosReflexos, _diasAviso, _meses];
    public override string NomeArquivoPdf => "sobreaviso-prontidao.pdf";

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
        var habitual = _habitualidade.Valor<bool>();
        var dispensa = _situacao.Valor<bool>();
        var dsr = habitual && _dsr.Valor<bool>();
        var incidencias = IncidenciasReflexo.Fgts | (dispensa ? IncidenciasReflexo.MultaFgts : IncidenciasReflexo.Nenhuma);
        if (habitual)
            incidencias |= IncidenciasReflexo.DecimoTerceiro | IncidenciasReflexo.Ferias
                | (dsr ? IncidenciasReflexo.Dsr : IncidenciasReflexo.Nenhuma)
                | (dispensa ? IncidenciasReflexo.AvisoPrevio : IncidenciasReflexo.Nenhuma);
        return await LerECalcularAsync(_simulador, leitor => new SimularSobreavisoRequest(new EntradaSobreaviso(
            meses.Valor, leitor.Inteiro(_divisor), habitual, incidencias,
            dsr && _dsrNosReflexos.Valor<bool>(),
            habitual && dispensa ? leitor.Inteiro(_diasAviso) : 0,
            FeriasGozadas: !dispensa, Admissao: habitual ? leitor.DataOpcional(_admissao) : null)), cancellationToken);
    }

    private void AtualizarCampos()
    {
        var habitual = _habitualidade.Valor<bool>();
        _dsr.Visivel = _admissao.Visivel = habitual;
        _dsrNosReflexos.Visivel = habitual && _dsr.Valor<bool>();
        _diasAviso.Visivel = habitual && _situacao.Valor<bool>();
    }
}
