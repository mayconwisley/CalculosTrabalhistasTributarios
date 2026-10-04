using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraGanhoCapital : CalculadoraBase
{
    private readonly ISimularDemonstrativoUseCase<SimularGanhoCapitalRequest> _simulador;
    private readonly CampoOpcaoViewModel _bem = new("Bem vendido",
        [
            new("Imóvel residencial", BemAlienado.ImovelResidencial),
            new("Outro imóvel (comercial, terreno)", BemAlienado.OutroImovel),
            new("Outro bem (veículo, participação, outros)", BemAlienado.OutroBem)
        ], "Os imóveis têm fatores de redução pelo tempo de posse; o residencial pode ter isenção pelo reinvestimento.");
    private readonly CampoTextoViewModel _aquisicao = new("Data da compra", TipoCampo.Data, DateTime.Today.AddYears(-10).ToString("dd/MM/yyyy", Cultura), "Data da escritura ou do contrato de compra (dd/mm/aaaa).");
    private readonly CampoTextoViewModel _venda = new("Data da venda", TipoCampo.Data, DateTime.Today.ToString("dd/MM/yyyy", Cultura), "Data do contrato de venda (dd/mm/aaaa).");
    private readonly CampoTextoViewModel _custo = Moeda("Custo de aquisição", "Valor de compra declarado no IRPF, com as benfeitorias.");
    private readonly CampoTextoViewModel _valorVenda = Moeda("Valor da venda", "Valor total da venda.");
    private readonly CampoTextoViewModel _despesas = Moeda("Despesas da venda", "Corretagem e outras despesas da venda pagas por você.");
    private readonly CampoOpcaoViewModel _unico = CampoOpcaoViewModel.SimNao("Único imóvel", false, "É o seu único imóvel, e você não vendeu outro imóvel nos últimos 5 anos: isento até R$ 440.000,00.");
    private readonly CampoTextoViewModel _reinvestido = Moeda("Reinvestido em 180 dias", "Valor da venda aplicado na compra de outro imóvel residencial no País em até 180 dias.");
    private readonly CampoTextoViewModel _vendasNoMes = Moeda("Vendas no mês", "Total vendido no mês em bens da mesma natureza, para a isenção de até R$ 35.000,00; deixe 0,00 para considerar só esta venda.");

    public CalculadoraGanhoCapital(ISimularDemonstrativoUseCase<SimularGanhoCapitalRequest> simulador)
    {
        _simulador = simulador;
        _bem.AoAlterar = () =>
        {
            var bem = _bem.Valor<BemAlienado>();
            _unico.Visivel = bem != BemAlienado.OutroBem;
            _reinvestido.Visivel = bem == BemAlienado.ImovelResidencial;
        };
        _bem.AoAlterar();
    }

    public override string Titulo => "Ganho de capital";
    public override string Descricao => "Calcule o IR na venda de um imóvel ou de outro bem, com as isenções e os fatores de redução.";
    public override string InstrucaoInicial => "Informe o bem, as datas e os valores da compra e da venda e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_bem, _aquisicao, _venda, _custo, _valorVenda, _despesas, _unico, _reinvestido, _vendasNoMes];
    public override string NomeArquivoPdf => $"ganho-de-capital-{_venda.Valor.Replace('/', '-')}.pdf";

    public override Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken) =>
        LerECalcularAsync(_simulador, leitor => new SimularGanhoCapitalRequest(_bem.Valor<BemAlienado>(), leitor.Data(_aquisicao), leitor.Data(_venda), leitor.Moeda(_custo),
            leitor.Moeda(_valorVenda), leitor.Moeda(_despesas), _unico.Visivel && _unico.Valor<bool>(), _reinvestido.Visivel ? leitor.Moeda(_reinvestido) : 0m, leitor.Moeda(_vendasNoMes)), cancellationToken);
}
