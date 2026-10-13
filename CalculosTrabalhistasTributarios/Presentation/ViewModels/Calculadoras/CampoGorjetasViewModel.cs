using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Gorjetas;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Competências de gorjetas: a parte do empregado no rateio de cada mês.</summary>
public sealed class CampoGorjetasViewModel : CampoCompetenciasViewModel<LinhaGorjetaViewModel>
{
    private string _valorMensal = "0,00";

    public CampoGorjetasViewModel()
        : base("Gorjetas por competência", "Parte do empregado no rateio das gorjetas de cada competência, antes da retenção.", MotorReflexos.MaximoDeMeses)
    {
    }

    public string ValorMensal { get => _valorMensal; set => SetProperty(ref _valorMensal, value); }

    protected override string? ValidarSugestoes() =>
        LeituraNumerica.TentarLer(ValorMensal, out var valor) && valor >= 0m ? null : "Informe as gorjetas por mês em reais, como 1.500,00.";

    protected override LinhaGorjetaViewModel CriarLinha(string competencia) => new() { Competencia = competencia, Valor = Moeda(ValorMensal) };

    public Result<IReadOnlyList<MesGorjeta>> Ler() => LerLinhas<MesGorjeta>((linha, competencia) =>
        LeituraNumerica.TentarLer(linha.Valor, out var valor)
            ? (Result<MesGorjeta>)new MesGorjeta(competencia, valor)
            : Erro.Validacao($"Revise as gorjetas de {linha}: use um valor monetário, por exemplo 1.500,00."),
        "Gere os meses ou adicione as competências de gorjetas.");

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(Inicio, Fim, ValorMensal,
        Linhas.Select(linha => new LinhaSalva(linha.Competencia, linha.Valor)).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados?.Inicio is null || dados.Fim is null || dados.ValorMensal is null || dados.Linhas is null || dados.Linhas.Length > Limite
                || dados.Linhas.Any(linha => linha?.Competencia is null || linha.Valor is null))
                return false;
            ValorMensal = dados.ValorMensal;
            Restaurar(dados.Inicio, dados.Fim, dados.Linhas.Select(linha => new LinhaGorjetaViewModel { Competencia = linha.Competencia, Valor = linha.Valor }));
            return true;
        }
        catch (JsonException) { return false; }
    }

    private sealed record LinhaSalva(string Competencia, string Valor);
    private sealed record DadosSalvos(string Inicio, string Fim, string ValorMensal, LinhaSalva[] Linhas);
}
