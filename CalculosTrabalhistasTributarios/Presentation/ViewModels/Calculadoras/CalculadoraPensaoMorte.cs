using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Previdencia;
using CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraPensaoMorte : CalculadoraBase
{
    // O aposentado já cumpriu a carência da aposentadoria: tem bem mais que as 18 contribuições exigidas para a duração da pensão.
    private const int ContribuicoesDoAposentado = 180;

    private readonly ISimularDemonstrativoUseCase<SimularPensaoMorteRequest> _simulador;
    private readonly CampoTextoViewModel _dataObito = new("Data do óbito", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura),
        "Data do óbito (dd/mm/aaaa): define o salário mínimo, o teto e a tabela de duração da pensão do cônjuge.");
    private readonly CampoOpcaoViewModel _situacao = new("Situação do segurado",
        [new("Aposentado", SituacaoInstituidor.Aposentado), new("Não aposentado", SituacaoInstituidor.NaoAposentado)],
        "Aposentado: a pensão parte da aposentadoria que recebia. Não aposentado: da aposentadoria por incapacidade permanente a que teria direito.") { Largura = 250 };
    private readonly CampoTextoViewModel _aposentadoria = Moeda("Aposentadoria recebida", "Valor mensal da aposentadoria do segurado na data do óbito.");
    private readonly CampoTextoViewModel _media = Moeda("Média dos salários", "Média de todos os salários de contribuição desde 07/1994, corrigidos.");
    private readonly CampoOpcaoViewModel _sexo = new("Sexo do segurado", [new("Homem", SexoSegurado.Masculino), new("Mulher", SexoSegurado.Feminino)],
        "Define o tempo a partir do qual o coeficiente da aposentadoria por incapacidade cresce: 20 anos para o homem e 15 para a mulher.");
    private readonly CampoTextoViewModel _anos = Inteiro("Contribuição do segurado: anos", 0, "Tempo de contribuição do segurado na data do óbito, em anos completos.");
    private readonly CampoTextoViewModel _meses = Inteiro("Contribuição: meses (0 a 11)", 0, "Os meses além dos anos completos, de 0 a 11.");
    private readonly CampoOpcaoViewModel _causa = new("Causa do óbito",
        [new("Doença ou causa comum", CausaObito.Comum), new("Acidente fora do trabalho", CausaObito.Acidente), new("Acidente do trabalho ou doença profissional", CausaObito.AcidenteTrabalho)],
        "O acidente dispensa as 18 contribuições e os 2 anos de união na duração da pensão do cônjuge; o do trabalho também leva a base a 100% da média.") { Largura = 330 };
    private readonly CampoOpcaoViewModel _conjuge = CampoOpcaoViewModel.SimNao("Deixou cônjuge?", true,
        "Cônjuge ou companheiro em união estável, inclusive o ex-cônjuge com pensão alimentícia.");
    private readonly CampoTextoViewModel _idadeConjuge = Inteiro("Idade do cônjuge no óbito", 45, "Idade do cônjuge ou companheiro na data do óbito, em anos completos: define a duração da cota dele.");
    private readonly CampoOpcaoViewModel _uniao = CampoOpcaoViewModel.SimNao("União de 2 anos ou mais?", true,
        "Casamento ou união estável com pelo menos 2 anos na data do óbito; com menos, a cota do cônjuge dura 4 meses, salvo óbito por acidente.");
    private readonly CampoTextoViewModel _outros = Inteiro("Outros dependentes", 0, "Filhos até 21 anos ou inválidos e os demais dependentes, além do cônjuge.");
    private readonly CampoOpcaoViewModel _invalido = CampoOpcaoViewModel.SimNao("Dependente inválido?", false,
        "Dependente inválido ou com deficiência intelectual, mental ou grave: a pensão é de 100% (EC 103/2019, art. 23, § 2º).");
    private readonly CampoTextoViewModel _beneficioConjuge = Moeda("Benefício do cônjuge (opcional)",
        "Aposentadoria ou pensão que o próprio cônjuge já recebe, para o redutor da acumulação. Deixe 0,00 se não houver.");

    public CalculadoraPensaoMorte(ISimularDemonstrativoUseCase<SimularPensaoMorteRequest> simulador)
    {
        _simulador = simulador;
        _situacao.AoAlterar = _conjuge.AoAlterar = AtualizarCampos;
        AtualizarCampos();
    }

    public override string Titulo => "Pensão por morte";
    public override string Descricao => "Calcule a pensão por morte do INSS: cota familiar, valor por dependente, duração para o cônjuge e o redutor da acumulação.";
    public override string InstrucaoInicial => "Informe o óbito, o segurado e os dependentes e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos =>
        [_dataObito, _situacao, _aposentadoria, _media, _sexo, _anos, _meses, _causa, _conjuge, _idadeConjuge, _uniao, _beneficioConjuge, _outros, _invalido];
    public override string NomeArquivoPdf => "pensao-por-morte.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var aposentado = _situacao.Valor<SituacaoInstituidor>() == SituacaoInstituidor.Aposentado;
        var conjuge = _conjuge.Valor<bool>();
        if (!aposentado && int.TryParse(_meses.Valor.Trim(), out var meses) && meses is < 0 or > 11)
        {
            _meses.MensagemErro = "Use de 0 a 11 meses.";
            return Task.FromResult<Result<DemonstrativoDto>>(Erro.Validacao($"O campo \"{_meses.Rotulo}\" aceita de 0 a 11 meses; some os anos completos no campo de anos."));
        }
        return LerECalcularAsync(_simulador, leitor => new SimularPensaoMorteRequest(
            leitor.Data(_dataObito), _situacao.Valor<SituacaoInstituidor>(),
            aposentado ? leitor.Moeda(_aposentadoria) : 0m, aposentado ? 0m : leitor.Moeda(_media), _sexo.Valor<SexoSegurado>(),
            aposentado ? ContribuicoesDoAposentado : leitor.Inteiro(_anos) * 12 + leitor.Inteiro(_meses), _causa.Valor<CausaObito>(),
            conjuge, conjuge ? leitor.Inteiro(_idadeConjuge) : 0, conjuge && _uniao.Valor<bool>(), leitor.Inteiro(_outros), _invalido.Valor<bool>(),
            conjuge ? leitor.Moeda(_beneficioConjuge) : 0m), cancellationToken);
    }

    private void AtualizarCampos()
    {
        var aposentado = _situacao.Valor<SituacaoInstituidor>() == SituacaoInstituidor.Aposentado;
        _aposentadoria.Visivel = aposentado;
        _media.Visivel = _sexo.Visivel = _anos.Visivel = _meses.Visivel = !aposentado;
        _idadeConjuge.Visivel = _uniao.Visivel = _beneficioConjuge.Visivel = _conjuge.Valor<bool>();
    }
}
