using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraSeguroDesemprego(ISimularDemonstrativoUseCase<SimularSeguroDesempregoRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _dispensa = new("Data da dispensa", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "Data do desligamento (dd/mm/aaaa), que define a tabela usada.");
    private readonly CampoTextoViewModel _ultimo = Moeda("Salário do último mês", "Salário do mês anterior à dispensa, com horas extras e adicionais.");
    private readonly CampoTextoViewModel _penultimo = Moeda("Salário do penúltimo mês", "Deixe 0,00 se não houve salário nesse mês: a média usa só os meses informados.");
    private readonly CampoTextoViewModel _antepenultimo = Moeda("Salário do antepenúltimo mês", "Deixe 0,00 se não houve salário nesse mês: a média usa só os meses informados.");
    private readonly CampoTextoViewModel _meses = Inteiro("Meses trabalhados", 12, "Meses com carteira assinada nos 36 meses antes da dispensa, em qualquer emprego; a fração de 15 dias ou mais conta como mês.");
    private readonly CampoOpcaoViewModel _solicitacao = new("Solicitação",
        [
            new("1ª solicitação", SolicitacaoSeguroDesemprego.Primeira),
            new("2ª solicitação", SolicitacaoSeguroDesemprego.Segunda),
            new("3ª ou seguinte", SolicitacaoSeguroDesemprego.TerceiraOuMais)
        ], "Quantas vezes o trabalhador já pediu o seguro-desemprego, contando esta.");

    public override string Titulo => "Seguro-desemprego";
    public override string Descricao => "Calcule o valor e a quantidade de parcelas do seguro-desemprego pela média dos últimos salários.";
    public override string InstrucaoInicial => "Informe a data da dispensa, os últimos salários, os meses trabalhados e a solicitação e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_dispensa, _ultimo, _penultimo, _antepenultimo, _meses, _solicitacao];
    public override string NomeArquivoPdf => $"seguro-desemprego-{_dispensa.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoSalario => _ultimo;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularSeguroDesempregoRequest(leitor.Data(_dispensa), [leitor.Moeda(_ultimo), leitor.Moeda(_penultimo), leitor.Moeda(_antepenultimo)],
            _solicitacao.Valor<SolicitacaoSeguroDesemprego>(), leitor.Inteiro(_meses)), cancellationToken);
}
