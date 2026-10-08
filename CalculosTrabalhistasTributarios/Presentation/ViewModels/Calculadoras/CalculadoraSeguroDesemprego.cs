using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraSeguroDesemprego : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularSeguroDesempregoRequest> _simulador;
    private readonly CampoOpcaoViewModel _vinculo = new("Vínculo",
        [new("Empregado (CLT)", false), new("Empregado doméstico", true)],
        "O doméstico recebe um salário mínimo por parcela, em até 3 parcelas, sem a tabela de faixas (LC 150/2015).");
    private readonly CampoTextoViewModel _dispensa = new("Data da dispensa", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "Data do desligamento (dd/mm/aaaa), que define a tabela usada.");
    private readonly CampoTextoViewModel _ultimo = Moeda("Salário do último mês", "Salário do mês anterior à dispensa, com horas extras e adicionais.");
    private readonly CampoTextoViewModel _penultimo = Moeda("Salário do penúltimo mês", "Deixe 0,00 se não houve salário nesse mês: a média usa só os meses informados.");
    private readonly CampoTextoViewModel _antepenultimo = Moeda("Salário do antepenúltimo mês", "Deixe 0,00 se não houve salário nesse mês: a média usa só os meses informados.");
    private readonly CampoTextoViewModel _meses = Inteiro("Meses trabalhados", 12, "Meses com carteira assinada nos 36 meses antes da dispensa, em qualquer emprego; a fração de 15 dias ou mais conta como mês. Doméstico: meses como doméstico nos últimos 24.");
    private readonly CampoTextoViewModel _meses18 = new("Meses com salário (18)", TipoCampo.Inteiro, "", "Na 1ª solicitação, conte apenas os meses com salário nos 18 meses anteriores à dispensa; são exigidos pelo menos 12.") { MostrarDica = true, DicaEmLinha = "1ª: mínimo 12 de 18 meses." };
    private readonly CampoTextoViewModel _meses12 = new("Meses com salário (12)", TipoCampo.Inteiro, "", "Na 2ª solicitação, conte apenas os meses com salário nos 12 meses anteriores à dispensa; são exigidos pelo menos 9.") { MostrarDica = true, DicaEmLinha = "2ª: mínimo 9 de 12 meses." };
    private readonly CampoTextoViewModel _meses6 = new("Meses com salário (6)", TipoCampo.Inteiro, "", "Da 3ª solicitação em diante, é exigido salário em cada um dos 6 meses imediatamente anteriores à dispensa.") { MostrarDica = true, DicaEmLinha = "3ª+: salário nos 6 meses." };
    private readonly CampoOpcaoViewModel _solicitacao = new("Solicitação",
        [
            new("1ª solicitação", SolicitacaoSeguroDesemprego.Primeira),
            new("2ª solicitação", SolicitacaoSeguroDesemprego.Segunda),
            new("3ª ou seguinte", SolicitacaoSeguroDesemprego.TerceiraOuMais)
        ], "Quantas vezes o trabalhador já pediu o seguro-desemprego, contando esta.");

    public CalculadoraSeguroDesemprego(ISimularDemonstrativoUseCase<SimularSeguroDesempregoRequest> simulador)
    {
        _simulador = simulador;
        _vinculo.AoAlterar = AjustarCampos;
        _solicitacao.AoAlterar = AjustarCampos;
        AjustarCampos();
    }

    public override string Titulo => "Seguro-desemprego";
    public override string Descricao => "Calcule o valor e a quantidade de parcelas do seguro-desemprego pela média dos últimos salários.";
    public override string InstrucaoInicial => "Informe os últimos salários, os meses nos últimos 36 e os meses com salário na janela da solicitação; depois selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_vinculo, _dispensa, _ultimo, _penultimo, _antepenultimo, _meses, _solicitacao, _meses18, _meses12, _meses6];
    public override string NomeArquivoPdf => $"seguro-desemprego-{_dispensa.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoSalario => _ultimo;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularSeguroDesempregoRequest(leitor.Data(_dispensa), [leitor.Moeda(_ultimo), leitor.Moeda(_penultimo), leitor.Moeda(_antepenultimo)],
            _solicitacao.Valor<SolicitacaoSeguroDesemprego>(), leitor.Inteiro(_meses), _vinculo.Valor<bool>(),
            _vinculo.Valor<bool>() ? null : leitor.Inteiro(CampoCarencia())), cancellationToken);

    private CampoTextoViewModel CampoCarencia() => _solicitacao.Valor<SolicitacaoSeguroDesemprego>() switch
    {
        SolicitacaoSeguroDesemprego.Primeira => _meses18,
        SolicitacaoSeguroDesemprego.Segunda => _meses12,
        _ => _meses6
    };

    private void AjustarCampos()
    {
        var domestico = _vinculo.Valor<bool>();
        var campoCarencia = CampoCarencia();
        _ultimo.Visivel = _penultimo.Visivel = _antepenultimo.Visivel = _solicitacao.Visivel = !domestico;
        _meses18.Visivel = !domestico && ReferenceEquals(campoCarencia, _meses18);
        _meses12.Visivel = !domestico && ReferenceEquals(campoCarencia, _meses12);
        _meses6.Visivel = !domestico && ReferenceEquals(campoCarencia, _meses6);
    }
}
