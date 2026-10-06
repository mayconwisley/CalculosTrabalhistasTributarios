using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraMediaVerbasVariaveis(ISimularDemonstrativoUseCase<SimularMediaVerbasVariaveisRequest> simulador) : CalculadoraBase
{
    private readonly CampoMediaVerbasVariaveisViewModel _campo = new();

    public override string Titulo => "Média de verbas variáveis";
    public override string Descricao => "Apure a média mensal de comissões, DSR, horas extras e adicionais por competência.";
    public override string InstrucaoInicial => "Escolha o período, gere os meses, preencha as verbas e confira o divisor antes de calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_campo];
    public override string NomeArquivoPdf => "media-verbas-variaveis.pdf";

    public override Dictionary<string, string> ExportarCampos() => new() { [_campo.Rotulo] = _campo.Exportar() };
    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        if (valores.TryGetValue(_campo.Rotulo, out var json)) _campo.Importar(json);
    }

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var entrada = _campo.Ler();
        return entrada.Falhou ? entrada.Erro : await simulador.ExecutarAsync(
            new SimularMediaVerbasVariaveisRequest(entrada.Valor.Meses, entrada.Valor.Divisor), cancellationToken);
    }
}
