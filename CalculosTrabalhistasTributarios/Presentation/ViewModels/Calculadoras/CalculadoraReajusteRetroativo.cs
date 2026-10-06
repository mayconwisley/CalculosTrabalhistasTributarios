using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraReajusteRetroativo(ISimularDemonstrativoUseCase<SimularReajusteRetroativoRequest> simulador) : CalculadoraBase
{
    private readonly CampoReajusteRetroativoViewModel _reajuste = new();

    public override string Titulo => "Diferenças de reajuste retroativo";
    public override string Descricao => "Compare o que foi pago com o salário reajustado em cada mês e apure os reflexos informados.";
    public override string InstrucaoInicial => "Informe o período, o salário anterior e o percentual. Gere os meses, revise as linhas e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_reajuste];
    public override string NomeArquivoPdf => "diferencas-reajuste-retroativo.pdf";

    public override Dictionary<string, string> ExportarCampos() => new() { [_reajuste.Rotulo] = _reajuste.Exportar() };

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        if (valores.TryGetValue(_reajuste.Rotulo, out var json))
            _reajuste.Importar(json);
    }

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var parcelas = _reajuste.Ler();
        return parcelas.Falhou
            ? parcelas.Erro
            : await simulador.ExecutarAsync(new SimularReajusteRetroativoRequest(parcelas.Valor), cancellationToken);
    }
}
