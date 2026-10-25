using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Os exemplos do botão "Preencher exemplo" são os dados fictícios das imagens do manual: cada um precisa abrir na sua
/// janela, calcular sem erro e, quando a imagem mostra o resultado, chegar aos mesmos valores.
/// </summary>
public class ExemplosTests
{
    /// <summary>Destaques do resultado mostrados na imagem do manual de cada calculadora, pelo rótulo.</summary>
    private static readonly IReadOnlyDictionary<string, (string Rotulo, string Valor)[]> Esperados = DestaquesDoManual.Esperados;

    private static readonly string[] JanelasProprias = ["Pensao", "PensaoAtraso", "DebitoJudicial", "Estabilidade", "Jornada"];

    public static TheoryData<string> Calculadoras() => new(ExemplosDoManual.Chaves.Where(chave => chave.StartsWith("Calculadora.", StringComparison.Ordinal)));

    public static TheoryData<string> Proprias() => new(ExemplosDoManual.Chaves.Where(JanelasProprias.Contains));

    [Fact]
    public void Toda_calculadora_tem_um_exemplo()
    {
        var esperadas = Enum.GetValues<TipoCalculadora>().Select(tipo => $"Calculadora.{tipo}").Concat(JanelasProprias);
        Assert.Empty(esperadas.Except(ExemplosDoManual.Chaves));
    }

    [Theory]
    [MemberData(nameof(Calculadoras))]
    public async Task Exemplo_da_calculadora_calcula_como_no_manual(string chave)
    {
        var servicos = await ServicosAsync();
        var calculadora = servicos.GetRequiredKeyedService<ICalculadora>(Enum.Parse<TipoCalculadora>(chave["Calculadora.".Length..]));
        var exemplo = ExemplosDoManual.Obter(chave)!;
        calculadora.ImportarCampos(exemplo.Campos);

        // Um rótulo que não existe seria ignorado em silêncio ao importar; o exemplo ficaria incompleto.
        Assert.Empty(exemplo.Campos.Keys.Except(calculadora.ExportarCampos().Keys));
        var resultado = await calculadora.CalcularAsync(CancellationToken.None);
        Assert.True(resultado.Sucesso, resultado.Sucesso ? null : resultado.Erro.Mensagem);
        foreach (var (rotulo, valor) in Esperados.GetValueOrDefault(chave, []))
            Assert.Equal(valor, Texto.Normalizar(resultado.Valor.Destaques.Single(destaque => destaque.Rotulo == rotulo).Valor));
    }

    // A base de INSS de cada exemplo, pela memória, contra os limites de 2026 (R$ 1.621,00, R$ 2.902,84, R$ 4.354,27 e
    // R$ 8.475,55): a referência é a alíquota da última faixa alcançada, como no holerite, e não a alíquota efetiva.
    [Theory]
    [InlineData("Calculadora.Holerite", "INSS", "12%")] // R$ 4.242,96
    [InlineData("Calculadora.HorasExtras", "INSS", "12%")] // R$ 3.127,38
    [InlineData("Calculadora.Adicionais", "INSS", "12%")] // R$ 3.900,00
    [InlineData("Calculadora.Comissoes", "INSS", "14%")] // R$ 6.100,00
    [InlineData("Calculadora.Domestico", "INSS", "9%")] // R$ 2.000,00
    [InlineData("Calculadora.SalarioPeloLiquido", "INSS", "14%")] // R$ 5.947,91
    [InlineData("Calculadora.DecimoTerceiro", "INSS sobre o 13º", "9%")] // R$ 2.875,00
    [InlineData("Calculadora.Rescisao", "INSS sobre o saldo de salário", "7,5%")] // R$ 1.500,00
    [InlineData("Calculadora.Rescisao", "INSS sobre o 13º", "7,5%")] // R$ 1.000,00
    [InlineData("Calculadora.Ferias", "INSS sobre as férias", "9% e 7,5%")] // R$ 2.240,00 em 01/2026 e R$ 1.493,33 em 02/2026
    public async Task Referencia_do_inss_e_a_faixa_alcancada(string chave, string descricao, string referencia)
    {
        var servicos = await ServicosAsync();
        var calculadora = servicos.GetRequiredKeyedService<ICalculadora>(Enum.Parse<TipoCalculadora>(chave["Calculadora.".Length..]));
        calculadora.ImportarCampos(ExemplosDoManual.Obter(chave)!.Campos);

        var resultado = await calculadora.CalcularAsync(CancellationToken.None);

        Assert.True(resultado.Sucesso, resultado.Sucesso ? null : resultado.Erro.Mensagem);
        Assert.Equal(referencia, resultado.Valor.Descontos.Single(desconto => desconto.Descricao == descricao).Referencia);
    }

    [Theory]
    [MemberData(nameof(Proprias))]
    public async Task Exemplo_da_janela_propria_abre_e_calcula(string chave)
    {
        var servicos = await ServicosAsync();
        ICalculoSalvavel tela = chave switch
        {
            "Pensao" => servicos.GetRequiredService<IPensaoViewModelFactory>().Criar(),
            "PensaoAtraso" => servicos.GetRequiredService<IPensaoAtrasoViewModelFactory>().Criar(),
            "DebitoJudicial" => servicos.GetRequiredService<IDebitoJudicialViewModelFactory>().Criar(),
            "Estabilidade" => servicos.GetRequiredService<IEstabilidadeViewModelFactory>().Criar(),
            _ => servicos.GetRequiredService<IJornadaViewModelFactory>().Criar()
        };
        var exemplo = ExemplosDoManual.Obter(chave)!;
        tela.ImportarDados(exemplo);
        await tela.RecalcularAsync();

        Assert.Empty(Notificador.Mensagens);
        var exportado = tela.ExportarDados();
        foreach (var (campo, valor) in exemplo.Campos)
            Assert.Equal(valor, exportado.Valor(campo, "(campo inexistente)"));
        Assert.Equal(exemplo.Linhas?.Count ?? 0, exportado.Linhas?.Count ?? 0);

        // Os resultados das imagens 06 (pensão) e 48 (jornada) do manual.
        if (tela is Presentation.ViewModels.PensaoViewModel pensao)
            Assert.Equal("R$ 2.111,99", Texto.Normalizar(pensao.IndicadoresResumo[0].Valor));
        if (tela is Presentation.ViewModels.JornadaViewModel jornada)
            Assert.Equal(("187:00", "3:00"), (jornada.Indicadores[0].Valor, jornada.Indicadores[1].Valor));
    }

    private static readonly NotificadorDeTeste Notificador = new();

    private static readonly Lazy<Task<IServiceProvider>> Servicos = new(async () =>
    {
        // O banco de testes já inicializado; as calculadoras e as janelas próprias vêm da composição do aplicativo.
        await Ambiente.ConsultaAsync();
        var colecao = new ServiceCollection().AddApplication().AddInfrastructure().AddPresentation().AddSingleton<IUserNotifier>(Notificador);
        return colecao.BuildServiceProvider().CreateScope().ServiceProvider;
    });

    private static Task<IServiceProvider> ServicosAsync() => Servicos.Value;

    private sealed class NotificadorDeTeste : IUserNotifier
    {
        public List<string> Mensagens { get; } = [];
        public void MostrarAviso(string mensagem, string titulo = "Dados inválidos") => Mensagens.Add(mensagem);
        public void MostrarErro(string mensagem, Exception exception) => Mensagens.Add($"{mensagem} {exception.Message}");
        public void MostrarFalha(Erro erro, string contexto) => Mensagens.Add($"{contexto} {erro.Mensagem}");
        public bool Confirmar(string mensagem, string titulo) => true;
    }
}
