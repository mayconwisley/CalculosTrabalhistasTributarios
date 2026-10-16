using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraAposentadoria : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularAposentadoriaRequest> _simulador;
    private readonly CampoOpcaoViewModel _sexo = new("Sexo", [new("Homem", SexoSegurado.Masculino), new("Mulher", SexoSegurado.Feminino)],
        "Os requisitos de idade e de tempo e o coeficiente do benefício são diferentes para homens e mulheres.");
    private readonly CampoTextoViewModel _nascimento = new("Data de nascimento", TipoCampo.Data, DateTime.Today.AddYears(-50).ToString("dd/MM/yyyy", Cultura),
        "Data de nascimento do segurado (dd/mm/aaaa).");
    private readonly CampoTextoViewModel _dataSimulacao = new("Data da simulação", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
        "Data a que se refere o tempo de contribuição informado; a projeção começa nela, com o salário mínimo e o teto do mês.");
    private readonly CampoTextoViewModel _anos = Inteiro("Contribuição até a data: anos", 0, "Tempo de contribuição até a data da simulação, como no CNIS: os anos completos.");
    private readonly CampoTextoViewModel _meses = Inteiro("Contribuição: meses (0 a 11)", 0, "Os meses além dos anos completos, de 0 a 11.");
    private readonly CampoTextoViewModel _anosReforma = Inteiro("Em 13/11/2019: anos", 0, "Tempo de contribuição até 13/11/2019, data da reforma. Use 0 se começou a contribuir depois dela.");
    private readonly CampoTextoViewModel _mesesReforma = Inteiro("Em 13/11/2019: meses (0 a 11)", 0, "Os meses além dos anos completos em 13/11/2019, de 0 a 11.");
    private readonly CamposMediaSalarios _media = new("a data da simulação",
        "Calcular: informe os salários de contribuição desde 07/1994, que são corrigidos pelo INPC até a data da simulação. Informar: use a média do simulador do Meu INSS.");
    private readonly CampoOpcaoViewModel _continua = CampoOpcaoViewModel.SimNao("Continua contribuindo?", true,
        "Sim: o tempo de contribuição cresce um mês a cada mês da projeção. Não: só a idade avança.");
    private readonly CampoTextoViewModel _expectativa = new("Sobrevida do IBGE (opcional)", TipoCampo.Numero, string.Empty,
        "Anos de expectativa de sobrevida na idade da aposentadoria, pela tábua do IBGE, para o fator previdenciário do pedágio de 50%. Deixe vazio se não souber.", permiteVazio: true);

    public CalculadoraAposentadoria(ISimularDemonstrativoUseCase<SimularAposentadoriaRequest> simulador)
    {
        _simulador = simulador;
        _media.Origem.AoAlterar = () => _media.AtualizarVisibilidade();
        _media.AtualizarVisibilidade();
    }

    public override string Titulo => "Simulação de aposentadoria";
    public override string Descricao => "Veja quando você cumpre cada regra de aposentadoria do INSS da reforma de 2019 e o benefício estimado em cada uma.";
    public override string InstrucaoInicial => "Informe o sexo, o nascimento, o tempo de contribuição até hoje e em 13/11/2019 e os salários de contribuição, ou a média pronta, e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_sexo, _nascimento, _dataSimulacao, _anos, _meses, _anosReforma, _mesesReforma, _continua, _expectativa, .. _media.Campos];

    public override Dictionary<string, string> ExportarCampos()
    {
        var campos = base.ExportarCampos();
        _media.Exportar(campos);
        return campos;
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        var atualizados = _media.Migrar(valores);
        base.ImportarCampos(atualizados);
        _media.ImportarSalarios(atualizados);
    }
    public override string NomeArquivoPdf => "simulacao-aposentadoria.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var salarios = _media.LerSalarios();
        if (salarios.Falhou)
            return Task.FromResult<Result<DemonstrativoDto>>(salarios.Erro);
        foreach (var campo in new[] { _meses, _mesesReforma })
            if (int.TryParse(campo.Valor.Trim(), out var meses) && meses is < 0 or > 11)
            {
                campo.MensagemErro = "Use de 0 a 11 meses.";
                return Task.FromResult<Result<DemonstrativoDto>>(Erro.Validacao($"O campo \"{campo.Rotulo}\" aceita de 0 a 11 meses; some os anos completos no campo de anos."));
            }
        return LerECalcularAsync(_simulador, leitor => new SimularAposentadoriaRequest(
            _sexo.Valor<SexoSegurado>(), leitor.Data(_nascimento), leitor.Data(_dataSimulacao),
            leitor.Inteiro(_anos) * 12 + leitor.Inteiro(_meses), leitor.Inteiro(_anosReforma) * 12 + leitor.Inteiro(_mesesReforma),
            _continua.Valor<bool>(), _media.Calcular ? 0m : leitor.Moeda(_media.Media),
            string.IsNullOrWhiteSpace(_expectativa.Valor) ? null : leitor.Numero(_expectativa), salarios.Valor), cancellationToken);
    }
}
