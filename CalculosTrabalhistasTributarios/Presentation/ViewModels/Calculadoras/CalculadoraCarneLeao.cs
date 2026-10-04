using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraCarneLeao(ISimularDemonstrativoUseCase<SimularCarneLeaoRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _competencia = Competencia("Competência", "Mês do recebimento (MM/AAAA): o carnê-leão segue o regime de caixa.");
    private readonly CampoTextoViewModel _rendimentos = Moeda("Rendimentos de pessoas físicas", "Honorários, consultas, aulas e outros rendimentos de trabalho recebidos de pessoas físicas ou do exterior.");
    private readonly CampoTextoViewModel _alugueis = Moeda("Aluguéis recebidos", "Aluguéis recebidos de pessoas físicas; os pagos por empresas têm IRRF retido por elas.");
    private readonly CampoTextoViewModel _despesasAluguel = Moeda("Despesas do aluguel", "IPTU, condomínio e taxa de administração pagos por você, como locador.");
    private readonly CampoTextoViewModel _livroCaixa = Moeda("Livro-caixa", "Despesas do consultório ou escritório escrituradas no livro-caixa, de quem trabalha por conta própria.");
    private readonly CampoTextoViewModel _inss = Moeda("INSS pago no mês", "Contribuição paga como contribuinte individual, em GPS ou no carnê do INSS.");
    private readonly CampoTextoViewModel _dependentes = Inteiro("Dependentes", 0, "Dependentes para a dedução, na modalidade de deduções legais.");
    private readonly CampoTextoViewModel _pensao = Moeda("Pensão alimentícia paga", "Pensão judicial ou por escritura pública paga no mês.");

    public override string Titulo => "Carnê-leão";
    public override string Descricao => "Calcule o IR mensal sobre rendimentos recebidos de pessoas físicas e do exterior, como honorários e aluguéis.";
    public override string InstrucaoInicial => "Informe a competência, os rendimentos e as deduções do mês e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_competencia, _rendimentos, _alugueis, _despesasAluguel, _livroCaixa, _inss, _dependentes, _pensao];
    public override string NomeArquivoPdf => $"carne-leao-{_competencia.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoCompetencia => _competencia;
    protected override CampoTextoViewModel CampoDependentes => _dependentes;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularCarneLeaoRequest(leitor.Competencia(_competencia), leitor.Moeda(_rendimentos), leitor.Moeda(_alugueis),
            leitor.Moeda(_despesasAluguel), leitor.Moeda(_livroCaixa), leitor.Moeda(_inss), leitor.Inteiro(_dependentes), leitor.Moeda(_pensao)), cancellationToken);
}
