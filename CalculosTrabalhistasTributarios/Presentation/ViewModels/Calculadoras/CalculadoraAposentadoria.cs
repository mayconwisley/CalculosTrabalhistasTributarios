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
    private readonly CampoOpcaoViewModel _origemMedia = new("Média dos salários",
        [new("Calcular pelos salários", true), new("Informar a média pronta", false)],
        "Calcular: informe os salários de contribuição desde 07/1994, que são corrigidos pelo INPC até a data da simulação. Informar: use a média do simulador do Meu INSS.") { Largura = 250 };
    private readonly CampoTextoViewModel _media = Moeda("Média informada",
        "Média de todos os salários de contribuição desde 07/1994, corrigidos, como no simulador do Meu INSS.");
    private readonly CampoSalariosContribuicaoViewModel _salarios = new();
    private readonly CampoOpcaoViewModel _continua = CampoOpcaoViewModel.SimNao("Continua contribuindo?", true,
        "Sim: o tempo de contribuição cresce um mês a cada mês da projeção. Não: só a idade avança.");
    private readonly CampoTextoViewModel _expectativa = new("Sobrevida do IBGE (opcional)", TipoCampo.Numero, string.Empty,
        "Anos de expectativa de sobrevida na idade da aposentadoria, pela tábua do IBGE, para o fator previdenciário do pedágio de 50%. Deixe vazio se não souber.", permiteVazio: true);

    public CalculadoraAposentadoria(ISimularDemonstrativoUseCase<SimularAposentadoriaRequest> simulador)
    {
        _simulador = simulador;
        _origemMedia.AoAlterar = AtualizarCampos;
        AtualizarCampos();
    }

    public override string Titulo => "Simulação de aposentadoria";
    public override string Descricao => "Veja quando você cumpre cada regra de aposentadoria do INSS da reforma de 2019 e o benefício estimado em cada uma.";
    public override string InstrucaoInicial => "Informe o sexo, o nascimento, o tempo de contribuição até hoje e em 13/11/2019 e os salários de contribuição, ou a média pronta, e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_sexo, _nascimento, _dataSimulacao, _anos, _meses, _anosReforma, _mesesReforma, _continua, _expectativa, _origemMedia, _media, _salarios];

    public override Dictionary<string, string> ExportarCampos()
    {
        var campos = base.ExportarCampos();
        campos[_salarios.Rotulo] = _salarios.Exportar();
        return campos;
    }

    // O histórico anterior guardava a média digitada em "Média dos salários", rótulo que hoje é a escolha da origem.
    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        var atualizados = new Dictionary<string, string>(valores);
        if (!atualizados.ContainsKey(_media.Rotulo) && atualizados.TryGetValue(_origemMedia.Rotulo, out var antigo) && _origemMedia.Opcoes.All(opcao => opcao.Texto != antigo))
        {
            atualizados[_media.Rotulo] = antigo;
            atualizados[_origemMedia.Rotulo] = _origemMedia.Opcoes[1].Texto;
        }
        base.ImportarCampos(atualizados);
        if (!atualizados.TryGetValue(_salarios.Rotulo, out var json) || !_salarios.Importar(json))
            _salarios.Linhas.Clear();
    }
    public override string NomeArquivoPdf => "simulacao-aposentadoria.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var calcular = _origemMedia.Valor<bool>();
        IReadOnlyList<PeriodoSalarioContribuicao>? salarios = null;
        if (calcular)
        {
            var lidos = _salarios.Ler();
            if (lidos.Falhou)
                return Task.FromResult<Result<DemonstrativoDto>>(lidos.Erro);
            salarios = lidos.Valor;
        }
        foreach (var campo in new[] { _meses, _mesesReforma })
            if (int.TryParse(campo.Valor.Trim(), out var meses) && meses is < 0 or > 11)
            {
                campo.MensagemErro = "Use de 0 a 11 meses.";
                return Task.FromResult<Result<DemonstrativoDto>>(Erro.Validacao($"O campo \"{campo.Rotulo}\" aceita de 0 a 11 meses; some os anos completos no campo de anos."));
            }
        return LerECalcularAsync(_simulador, leitor => new SimularAposentadoriaRequest(
            _sexo.Valor<SexoSegurado>(), leitor.Data(_nascimento), leitor.Data(_dataSimulacao),
            leitor.Inteiro(_anos) * 12 + leitor.Inteiro(_meses), leitor.Inteiro(_anosReforma) * 12 + leitor.Inteiro(_mesesReforma),
            _continua.Valor<bool>(), calcular ? 0m : leitor.Moeda(_media),
            string.IsNullOrWhiteSpace(_expectativa.Valor) ? null : leitor.Numero(_expectativa), salarios), cancellationToken);
    }

    private void AtualizarCampos()
    {
        var calcular = _origemMedia.Valor<bool>();
        _salarios.Visivel = calcular;
        _media.Visivel = !calcular;
    }
}
