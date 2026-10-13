using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Sobreaviso;
using System.Globalization;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Competências de sobreaviso e prontidão: o salário normal, as horas de cada regime e os feriados do mês.</summary>
public sealed class CampoSobreavisoViewModel : CampoCompetenciasViewModel<LinhaSobreavisoViewModel>
{
    private string _salarioMensal = "0,00";
    private string _sobreavisoMensal = "0:00";
    private string _prontidaoMensal = "0:00";

    public CampoSobreavisoViewModel()
        : base("Competências de sobreaviso", "Salário, horas de sobreaviso e de prontidão e feriados de cada competência.", MotorReflexos.MaximoDeMeses)
    {
    }

    public string SalarioMensal { get => _salarioMensal; set => SetProperty(ref _salarioMensal, value); }
    public string SobreavisoMensal { get => _sobreavisoMensal; set => SetProperty(ref _sobreavisoMensal, value); }
    public string ProntidaoMensal { get => _prontidaoMensal; set => SetProperty(ref _prontidaoMensal, value); }

    protected override string? ValidarSugestoes() =>
        !LeituraNumerica.TentarLer(SalarioMensal, out var salario) || salario < 0m || !TentarHoras(SobreavisoMensal, out _) || !TentarHoras(ProntidaoMensal, out _)
            ? "Informe o salário em reais, como 3.300,00, e as horas como 40:00 ou 40,5."
            : null;

    protected override LinhaSobreavisoViewModel CriarLinha(string competencia) => new()
    {
        Competencia = competencia,
        Salario = Moeda(SalarioMensal),
        Sobreaviso = TentarHoras(SobreavisoMensal, out var sobreaviso) ? FormatarHoras(sobreaviso) : "0:00",
        Prontidao = TentarHoras(ProntidaoMensal, out var prontidao) ? FormatarHoras(prontidao) : "0:00"
    };

    public Result<IReadOnlyList<MesSobreaviso>> Ler() => LerLinhas<MesSobreaviso>((linha, competencia) =>
    {
        if (!LeituraNumerica.TentarLer(linha.Salario, out var salario))
            return Erro.Validacao($"Revise o salário de {linha}: use um valor monetário, por exemplo 3.300,00.");
        if (!TentarHoras(linha.Sobreaviso, out var sobreaviso) || !TentarHoras(linha.Prontidao, out var prontidao))
            return Erro.Validacao($"Revise as horas de {linha}: use horas e minutos, como 40:00, ou horas decimais, como 40,5.");
        if (!int.TryParse(linha.Feriados.Trim(), NumberStyles.Integer, Cultura, out var feriados) || feriados < 0)
            return Erro.Validacao($"Revise os feriados de {linha}: use um número inteiro, como 1.");
        return new MesSobreaviso(competencia, salario, sobreaviso, prontidao, feriados);
    }, "Gere os meses ou adicione as competências de sobreaviso e prontidão.");

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(Inicio, Fim, SalarioMensal, SobreavisoMensal, ProntidaoMensal,
        Linhas.Select(linha => new LinhaSalva(linha.Competencia, linha.Salario, linha.Sobreaviso, linha.Prontidao, linha.Feriados)).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados?.Inicio is null || dados.Fim is null || dados.SalarioMensal is null || dados.SobreavisoMensal is null || dados.ProntidaoMensal is null
                || dados.Linhas is null || dados.Linhas.Length > Limite
                || dados.Linhas.Any(linha => linha?.Competencia is null || linha.Salario is null || linha.Sobreaviso is null || linha.Prontidao is null || linha.Feriados is null))
                return false;
            SalarioMensal = dados.SalarioMensal;
            SobreavisoMensal = dados.SobreavisoMensal;
            ProntidaoMensal = dados.ProntidaoMensal;
            Restaurar(dados.Inicio, dados.Fim, dados.Linhas.Select(linha => new LinhaSobreavisoViewModel
                { Competencia = linha.Competencia, Salario = linha.Salario, Sobreaviso = linha.Sobreaviso, Prontidao = linha.Prontidao, Feriados = linha.Feriados }));
            return true;
        }
        catch (JsonException) { return false; }
    }

    private sealed record LinhaSalva(string Competencia, string Salario, string Sobreaviso, string Prontidao, string Feriados);
    private sealed record DadosSalvos(string Inicio, string Fim, string SalarioMensal, string SobreavisoMensal, string ProntidaoMensal, LinhaSalva[] Linhas);
}
