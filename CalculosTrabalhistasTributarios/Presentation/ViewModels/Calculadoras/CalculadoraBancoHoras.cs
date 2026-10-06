using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraBancoHoras(ISimularDemonstrativoUseCase<SimularBancoHorasRequest> simulador) : CalculadoraBase
{
    private readonly CampoBancoHorasViewModel _campo = new();

    public override string Titulo => "Banco de horas";
    public override string Descricao => "Concilie horas trabalhadas e compensadas em um ciclo e estime a quitação do saldo positivo.";
    public override string InstrucaoInicial => "Informe o regime, o período e as horas de cada movimento. O saldo é apurado em minutos.";
    public override IReadOnlyList<CampoViewModel> Campos => [_campo];
    public override string NomeArquivoPdf => "banco-de-horas.pdf";

    public override Dictionary<string, string> ExportarCampos() => new() { [_campo.Rotulo] = _campo.Exportar() };
    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        if (valores.TryGetValue(_campo.Rotulo, out var json)) _campo.Importar(json);
    }

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var entrada = _campo.Ler();
        if (entrada.Falhou) return entrada.Erro;
        var dados = entrada.Valor;
        return await simulador.ExecutarAsync(new SimularBancoHorasRequest(dados.Inicio, dados.Fim,
            dados.Regime, dados.Situacao, dados.Salario, dados.Divisor, dados.Adicional, dados.Lancamentos), cancellationToken);
    }
}
