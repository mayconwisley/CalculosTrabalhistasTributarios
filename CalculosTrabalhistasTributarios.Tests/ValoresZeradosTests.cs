using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Sem o valor que dá origem ao cálculo, nenhuma calculadora apresenta um resultado: o formulário recém-aberto, com os
/// valores zerados, e o exemplo do manual sem o valor principal terminam numa mensagem que diz o que informar.
/// </summary>
public class ValoresZeradosTests
{
    private static readonly Lazy<Task> Banco = new(Ambiente.ConsultaAsync);

    public static TheoryData<TipoCalculadora> Tipos() => new(Enum.GetValues<TipoCalculadora>());

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Formulario_recem_aberto_nao_calcula_com_valores_zerados(TipoCalculadora tipo)
    {
        var calculadora = await CriarAsync(tipo);

        var resultado = await calculadora.CalcularAsync(CancellationToken.None);

        Assert.True(resultado.Falhou, resultado.Falhou ? null : $"{tipo} calculou um resultado sem valores informados.");
    }

    // O exemplo do manual com o valor principal zerado: um campo pelo rótulo ou, nas grades, os textos que casam com o padrão.
    [Theory]
    [InlineData(TipoCalculadora.SalarioPeloLiquido, "Salário líquido desejado", ".+", "0,00", "Informe o salário líquido desejado")]
    [InlineData(TipoCalculadora.DecimoTerceiro, null, @"\d{1,3}(\.\d{3})*,\d{2}", "0,00", "Informe o salário ou as médias")]
    [InlineData(TipoCalculadora.Ferias, null, @"\d{1,3}(\.\d{3})*,\d{2}", "0,00", "Informe o salário ou as médias")]
    [InlineData(TipoCalculadora.HorasExtras, "Salário", ".+", "0,00", "Informe o salário")]
    [InlineData(TipoCalculadora.HorasExtras, null, @"^\d+:\d\d$", "0:00", "Informe as horas extras ou as horas noturnas")]
    [InlineData(TipoCalculadora.Rescisao, "Salário", ".+", "0,00", "Informe o salário do empregado")]
    [InlineData(TipoCalculadora.CustoFuncionario, "Salário", ".+", "0,00", "Informe o salário do empregado")]
    [InlineData(TipoCalculadora.ProLaboreAutonomo, "Valor bruto", ".+", "0,00", "Informe o valor bruto")]
    [InlineData(TipoCalculadora.Adicionais, "Salário", ".+", "0,00", "Informe o salário do empregado")]
    [InlineData(TipoCalculadora.SalarioFamilia, "Remuneração do mês cheio", ".+", "0,00", "Informe a remuneração do mês cheio")]
    [InlineData(TipoCalculadora.Plr, "Valor da PLR", ".+", "0,00", "Informe o valor da PLR")]
    [InlineData(TipoCalculadora.RevisaoPensao, "Valor bruto", ".+", "0,00", "Informe o valor bruto")]
    [InlineData(TipoCalculadora.IrpfAnual, null, @"\d{1,3}(\.\d{3})*,\d{2}", "0,00", "Informe os rendimentos do ano")]
    [InlineData(TipoCalculadora.AbonoSalarial, "Remuneração média mensal", ".+", "0,00", "Informe a remuneração média mensal")]
    [InlineData(TipoCalculadora.MediaVerbasVariaveis, null, @"\d{1,3}(\.\d{3})*,\d{2}", "0,00", "Informe os valores das verbas variáveis")]
    [InlineData(TipoCalculadora.ConferenciaFgts, null, @"\d{1,3}(\.\d{3})*,\d{2}", "0,00", "Informe a remuneração ou o depósito")]
    [InlineData(TipoCalculadora.SimplesNacional, "Receita bruta do mês", ".+", "0,00", "Informe a receita bruta do mês")]
    [InlineData(TipoCalculadora.ReajusteRetroativo, null, @"3\.[123]00,00", "3.000,00", "Nenhum lançamento tem diferença a pagar")]
    [InlineData(TipoCalculadora.SimulacaoTributaria, "Valor bruto", ".+", "0,00", "Valor bruto: informe um valor maior que zero")]
    public async Task Exemplo_sem_o_valor_principal_pede_o_valor(TipoCalculadora tipo, string? rotulo, string padrao, string substituto, string mensagem)
    {
        var calculadora = await CriarAsync(tipo);
        var exemplo = ExemplosDoManual.Obter($"Calculadora.{tipo}")!;
        Assert.True((await CalcularAsync(calculadora, exemplo.Campos)).Sucesso);

        var zerados = exemplo.Campos.ToDictionary(campo => campo.Key,
            campo => rotulo is null || campo.Key == rotulo ? Regex.Replace(campo.Value, padrao, substituto) : campo.Value);
        var resultado = await CalcularAsync(calculadora, zerados);

        Assert.True(resultado.Falhou, $"{tipo} calculou com o valor principal zerado.");
        Assert.StartsWith(mensagem, resultado.Erro.Mensagem);
    }

    private static async Task<ICalculadora> CriarAsync(TipoCalculadora tipo)
    {
        await Banco.Value;
        var servicos = new ServiceCollection().AddApplication().AddInfrastructure().AddPresentation().BuildServiceProvider().CreateScope().ServiceProvider;
        return servicos.GetRequiredKeyedService<ICalculadora>(tipo);
    }

    private static Task<Domain.Comum.Result<Application.DTOs.DemonstrativoDto>> CalcularAsync(ICalculadora calculadora, IReadOnlyDictionary<string, string> campos)
    {
        calculadora.ImportarCampos(campos);
        return calculadora.CalcularAsync(CancellationToken.None);
    }
}
