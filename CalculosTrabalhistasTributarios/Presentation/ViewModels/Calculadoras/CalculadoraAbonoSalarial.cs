using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraAbonoSalarial(ISimularDemonstrativoUseCase<SimularAbonoSalarialRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _anoBase = Inteiro("Ano-base", DateTime.Today.Year - 2, "Ano trabalhado; o abono é pago dois anos depois (o de 2024, em 2026).");
    private readonly CampoTextoViewModel _meses = Inteiro("Meses trabalhados", 12, "Meses com carteira assinada no ano-base; a fração de 15 dias ou mais conta como mês.");
    private readonly CampoTextoViewModel _media = Moeda("Remuneração média mensal", "Média das remunerações recebidas no ano-base.");
    private readonly CampoOpcaoViewModel _cadastro = CampoOpcaoViewModel.SimNao("Cadastrado há 5 anos", true, "Inscrito no PIS/Pasep há pelo menos 5 anos.");
    private readonly CampoTextoViewModel _limite = Moeda("Limite de renda", "Limite da média do calendário; deixe 0,00 para usar o publicado (R$ 2.766,00 em 2026).");

    public override string Titulo => "Abono salarial (PIS/Pasep)";
    public override string Descricao => "Veja se há direito ao abono salarial e o valor pelos meses trabalhados no ano-base.";
    public override string InstrucaoInicial => "Informe o ano-base, os meses trabalhados e a remuneração média e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_anoBase, _meses, _media, _cadastro, _limite];
    public override string NomeArquivoPdf => $"abono-salarial-{_anoBase.Valor.Trim()}.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularAbonoSalarialRequest(leitor.Inteiro(_anoBase), leitor.Inteiro(_meses), leitor.Moeda(_media),
            _cadastro.Valor<bool>(), leitor.Moeda(_limite)), cancellationToken);
}
