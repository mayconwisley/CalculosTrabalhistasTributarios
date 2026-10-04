using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraAfastamento : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularAfastamentoRequest> _simulador;
    private readonly CampoOpcaoViewModel _tipo = new("Tipo",
        [
            new("Doença", TipoAfastamento.Doenca),
            new("Acidente de trabalho", TipoAfastamento.AcidenteDeTrabalho),
            new("Licença-maternidade", TipoAfastamento.Maternidade),
            new("Licença-paternidade", TipoAfastamento.Paternidade)
        ], "Na doença e no acidente, a empresa paga os 15 primeiros dias e o INSS, o restante.");
    private readonly CampoTextoViewModel _inicio = new("Início", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "Primeiro dia do afastamento ou da licença (dd/mm/aaaa).");
    private readonly CampoTextoViewModel _dias = Inteiro("Dias de afastamento", 30, "Dias do atestado ou da perícia, contando o primeiro.");
    private readonly CampoTextoViewModel _remuneracao = Moeda("Remuneração", "Salário mensal com os adicionais fixos.");
    private readonly CampoTextoViewModel _media = Moeda("Média dos salários de contribuição", "Média usada para estimar o auxílio do INSS; deixe 0,00 para usar a remuneração.");
    private readonly CampoOpcaoViewModel _empresaCidada = CampoOpcaoViewModel.SimNao("Empresa Cidadã", false, "Empresa no Programa Empresa Cidadã: +60 dias de licença-maternidade e +15 de licença-paternidade.");

    public CalculadoraAfastamento(ISimularDemonstrativoUseCase<SimularAfastamentoRequest> simulador)
    {
        _simulador = simulador;
        _tipo.AoAlterar = () =>
        {
            var licenca = _tipo.Valor<TipoAfastamento>() is TipoAfastamento.Maternidade or TipoAfastamento.Paternidade;
            _dias.Visivel = _media.Visivel = !licenca;
            _empresaCidada.Visivel = licenca;
        };
        _tipo.AoAlterar();
    }

    public override string Titulo => "Afastamentos e licenças";
    public override string Descricao => "Veja quanto a empresa e o INSS pagam na doença, no acidente de trabalho e nas licenças-maternidade e paternidade.";
    public override string InstrucaoInicial => "Escolha o tipo, informe o início, os dias e a remuneração e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_tipo, _inicio, _dias, _remuneracao, _media, _empresaCidada];
    public override string NomeArquivoPdf => $"afastamento-{_inicio.Valor.Replace('/', '-')}.pdf";

    protected override CampoTextoViewModel CampoSalario => _remuneracao;

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularAfastamentoRequest(_tipo.Valor<TipoAfastamento>(), leitor.Data(_inicio),
            _dias.Visivel ? leitor.Inteiro(_dias) : 0, leitor.Moeda(_remuneracao), _media.Visivel ? leitor.Moeda(_media) : 0m, _empresaCidada.Visivel && _empresaCidada.Valor<bool>()), cancellationToken);
}
