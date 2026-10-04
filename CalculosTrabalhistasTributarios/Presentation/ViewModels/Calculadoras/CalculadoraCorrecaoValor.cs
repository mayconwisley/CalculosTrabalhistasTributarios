using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Judicial;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraCorrecaoValor(ISimularDemonstrativoUseCase<SimularCorrecaoValorRequest> simulador) : CalculadoraBase
{
    private readonly CampoTextoViewModel _valor = Moeda("Valor", "Valor na data em que era devido.");
    private readonly CampoTextoViewModel _inicio = new("Mês em que era devido", TipoCampo.Competencia, DateTime.Today.AddYears(-1).ToString("MM/yyyy", Cultura), "Mês e ano do valor original (MM/AAAA): a correção começa nele.");
    private readonly CampoTextoViewModel _fim = Competencia("Mês da atualização", "Mês e ano para o qual o valor é atualizado (MM/AAAA): a correção vai até o mês anterior.");
    private readonly CampoOpcaoViewModel _indice = new("Índice",
        [
            new("IPCA (inflação oficial)", IndiceEconomico.Ipca),
            new("INPC", IndiceEconomico.Inpc),
            new("IPCA-E", IndiceEconomico.IpcaE),
            new("Selic", IndiceEconomico.Selic),
            new("TR", IndiceEconomico.Tr),
            new("Taxa legal", IndiceEconomico.TaxaLegal)
        ], "Índice previsto no contrato ou na decisão; sem previsão, o IPCA é o índice legal desde a Lei 14.905/2024.");
    private readonly CampoTextoViewModel _juros = new("Juros ao mês (%)", TipoCampo.Numero, "0", "Juros simples sobre o valor corrigido, como 1% ao mês de um contrato; 0 para só corrigir.");
    private readonly CampoTextoViewModel _multa = new("Multa (%)", TipoCampo.Numero, "0", "Multa sobre o valor corrigido, como os 2% de uma conta em atraso; 0 quando não há.");

    public override string Titulo => "Correção de valores";
    public override string Descricao => "Atualize um valor pelo IPCA, INPC, IPCA-E, Selic, TR ou taxa legal, com juros e multa opcionais.";
    public override string InstrucaoInicial => "Informe o valor, o mês em que era devido, o mês da atualização e o índice e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_valor, _inicio, _fim, _indice, _juros, _multa];
    public override string NomeArquivoPdf => $"correcao-de-valores-{_fim.Valor.Replace('/', '-')}.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularCorrecaoValorRequest(leitor.Moeda(_valor), leitor.Competencia(_inicio), leitor.Competencia(_fim),
            _indice.Valor<IndiceEconomico>(), leitor.Numero(_juros), leitor.Numero(_multa)), cancellationToken);
}
