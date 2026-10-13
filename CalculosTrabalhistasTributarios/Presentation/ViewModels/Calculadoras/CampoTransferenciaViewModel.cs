using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Reflexos;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Transferencia;
using System.Globalization;
using System.Text.Json;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Competências da transferência provisória: o salário da localidade de origem e os dias transferido.</summary>
public sealed class CampoTransferenciaViewModel : CampoCompetenciasViewModel<LinhaTransferenciaViewModel>
{
    private string _salarioMensal = "0,00";

    public CampoTransferenciaViewModel()
        : base("Competências da transferência", "Salário da origem e dias transferido em cada competência.", MotorReflexos.MaximoDeMeses)
    {
    }

    public string SalarioMensal { get => _salarioMensal; set => SetProperty(ref _salarioMensal, value); }

    protected override string? ValidarSugestoes() =>
        LeituraNumerica.TentarLer(SalarioMensal, out var salario) && salario >= 0m ? null : "Informe o salário por mês em reais, como 4.000,00.";

    protected override LinhaTransferenciaViewModel CriarLinha(string competencia) => new() { Competencia = competencia, Salario = Moeda(SalarioMensal) };

    public Result<IReadOnlyList<MesTransferencia>> Ler() => LerLinhas<MesTransferencia>((linha, competencia) =>
    {
        if (!LeituraNumerica.TentarLer(linha.Salario, out var salario))
            return Erro.Validacao($"Revise o salário de {linha}: use um valor monetário, por exemplo 4.000,00.");
        if (!int.TryParse(linha.Dias.Trim(), NumberStyles.Integer, Cultura, out var dias))
            return Erro.Validacao($"Revise os dias transferido em {linha}: use um número inteiro de 0 a 30.");
        return new MesTransferencia(competencia, salario, dias);
    }, "Gere os meses ou adicione as competências da transferência.");

    public string Exportar() => JsonSerializer.Serialize(new DadosSalvos(Inicio, Fim, SalarioMensal,
        Linhas.Select(linha => new LinhaSalva(linha.Competencia, linha.Salario, linha.Dias)).ToArray()));

    public bool Importar(string? json)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosSalvos>(json ?? "");
            if (dados?.Inicio is null || dados.Fim is null || dados.SalarioMensal is null || dados.Linhas is null || dados.Linhas.Length > Limite
                || dados.Linhas.Any(linha => linha?.Competencia is null || linha.Salario is null || linha.Dias is null))
                return false;
            SalarioMensal = dados.SalarioMensal;
            Restaurar(dados.Inicio, dados.Fim, dados.Linhas.Select(linha => new LinhaTransferenciaViewModel { Competencia = linha.Competencia, Salario = linha.Salario, Dias = linha.Dias }));
            return true;
        }
        catch (JsonException) { return false; }
    }

    private sealed record LinhaSalva(string Competencia, string Salario, string Dias);
    private sealed record DadosSalvos(string Inicio, string Fim, string SalarioMensal, LinhaSalva[] Linhas);
}
