using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraTributoAtraso(ISimularDemonstrativoUseCase<SimularTributoAtrasoRequest> simulador) : CalculadoraBase
{
    private readonly CampoOpcaoViewModel _guia = new("Guia",
        [
            new("DARF (tributos federais)", GuiaDeRecolhimento.Darf),
            new("DAS (Simples Nacional)", GuiaDeRecolhimento.Das),
            new("DAE (empregador doméstico)", GuiaDeRecolhimento.Dae),
            new("GPS (INSS)", GuiaDeRecolhimento.Gps)
        ], "Todas seguem a mesma regra de multa e juros.");
    private readonly CampoTextoViewModel _principal = Moeda("Valor principal", "Valor da guia no vencimento, sem acréscimos.");
    private readonly CampoTextoViewModel _vencimento = new("Vencimento", TipoCampo.Data, DateTime.Today.AddMonths(-1).ToString("dd/MM/yyyy", Cultura), "Data de vencimento, já prorrogada para o dia útil seguinte quando cai em fim de semana ou feriado.");
    private readonly CampoTextoViewModel _pagamento = new("Pagamento", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "Data em que a guia será paga.");

    public override string Titulo => "Tributo em atraso";
    public override string Descricao => "Calcule a multa e os juros de um DARF, DAS, DAE ou GPS pago depois do vencimento.";
    public override string InstrucaoInicial => "Escolha a guia, informe o valor, o vencimento e a data do pagamento e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_guia, _principal, _vencimento, _pagamento];
    public override string NomeArquivoPdf => $"tributo-em-atraso-{_pagamento.Valor.Replace('/', '-')}.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(simulador, leitor => new SimularTributoAtrasoRequest(_guia.Valor<GuiaDeRecolhimento>(), leitor.Moeda(_principal), leitor.Data(_vencimento), leitor.Data(_pagamento)), cancellationToken);
}
