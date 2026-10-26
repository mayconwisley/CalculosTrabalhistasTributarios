using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// Um erro de preenchimento aparece do mesmo jeito em todas as calculadoras da janela padrão: o campo fica destacado com
/// o que corrigir logo abaixo dele, e o aviso junto do Calcular resume o problema.
/// </summary>
public class ErrosNoFormularioTests
{
    private static readonly Lazy<Task> Banco = new(Ambiente.ConsultaAsync);

    public static TheoryData<TipoCalculadora> Tipos() => new(Enum.GetValues<TipoCalculadora>());

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Valor_negativo_destaca_o_proprio_campo(TipoCalculadora tipo)
    {
        var exemplo = ExemplosDoManual.Obter($"Calculadora.{tipo}")!;
        var numericos = Criar(tipo).Campos.OfType<CampoTextoViewModel>()
            .Where(campo => campo.Tipo is TipoCampo.Moeda or TipoCampo.Numero or TipoCampo.Inteiro or TipoCampo.Horas)
            .Select(campo => campo.Rotulo)
            .ToList();

        foreach (var rotulo in numericos)
        {
            var calculadora = Criar(tipo);
            calculadora.ImportarCampos(exemplo.Campos);
            var campo = calculadora.Campos.OfType<CampoTextoViewModel>().Single(item => item.Rotulo == rotulo);
            if (!campo.Visivel)
                continue;
            campo.Valor = campo.Tipo == TipoCampo.Horas ? "-1,5" : "-1";

            var janela = await CalcularAsync(tipo, calculadora);

            Assert.False(janela.TemResultado, $"{tipo}: calculou com {rotulo} negativo.");
            Assert.Equal([campo], calculadora.Campos.Where(item => item.TemErro));
            // Os meses de 0 a 11 da aposentadoria e da pensão por morte têm uma orientação própria, que já cobre o negativo.
            Assert.NotNull(campo.MensagemErro);
            Assert.Contains($"\"{rotulo}\"", janela.Aviso);
        }
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Formulario_vazio_destaca_o_que_falta_informar(TipoCalculadora tipo)
    {
        var calculadora = Criar(tipo);

        var janela = await CalcularAsync(tipo, calculadora);

        Assert.True(janela.TemAviso, $"{tipo}: o cálculo sem dados não deixou aviso.");
        Assert.Contains(calculadora.Campos, campo => campo.Visivel && campo.TemErro);
    }

    [Fact]
    public void Citacao_pelo_rotulo_mostra_so_a_orientacao_no_campo()
    {
        var valor = Campo("Valor solicitado (R$)");
        var juros = Campo("Juros (% ao mês)");

        var citados = CamposCitados.Encontrar("Valor solicitado: informe um valor maior que zero.", [juros, valor]);

        Assert.Equal([(valor, "Informe um valor maior que zero.")], citados);
    }

    [Fact]
    public void Rotulo_no_texto_aponta_o_campo_e_o_rotulo_maior_prevalece()
    {
        var salario = Campo("Salário");
        var liquido = Campo("Salário líquido desejado");
        const string mensagem = "Informe o salário líquido desejado, maior que zero.";

        Assert.Equal([(liquido, mensagem)], CamposCitados.Encontrar(mensagem, [salario, liquido]));
    }

    [Fact]
    public void Mensagem_com_dois_rotulos_aponta_os_dois_campos()
    {
        var salario = Campo("Salário");
        var medias = Campo("Médias de variáveis");
        var dependentes = Campo("Dependentes");
        const string mensagem = "Informe o salário ou as médias de variáveis que formam a base do 13º.";

        Assert.Equal([salario, medias], CamposCitados.Encontrar(mensagem, [salario, medias, dependentes]).Select(citado => citado.Campo));
    }

    [Fact]
    public void Rotulo_depois_de_preposicao_e_trecho_sobre_negativos_nao_apontam_campos()
    {
        var anoBase = Campo("Ano-base");
        var dependentes = Campo("Dependentes");

        Assert.Empty(CamposCitados.Encontrar("Informe a remuneração média mensal do ano-base, maior que zero.", [anoBase]));
        Assert.Empty(CamposCitados.Encontrar("Informe comissões maiores que zero; salário e dependentes não podem ser negativos.", [dependentes]));
    }

    [Fact]
    public void Mensagem_sobre_as_linhas_aponta_a_unica_grade()
    {
        var salario = Campo("Divisor do salário-hora");
        var grade = new CampoSobreavisoViewModel();

        Assert.Equal([grade], CamposCitados.Encontrar("Gere os meses ou adicione as competências de sobreaviso e prontidão.", [salario, grade]).Select(citado => citado.Campo));
        Assert.Empty(CamposCitados.Encontrar("Informe o sexo do segurado.", [salario, grade]));
    }

    [Fact]
    public void Campo_oculto_nao_e_apontado()
    {
        var salario = Campo("Salário");
        salario.Visivel = false;

        Assert.Empty(CamposCitados.Encontrar("Informe o salário do empregado.", [salario]));
    }

    private static CampoTextoViewModel Campo(string rotulo) => new(rotulo, TipoCampo.Moeda, "0,00");

    private static ICalculadora Criar(TipoCalculadora tipo) =>
        new ServiceCollection().AddApplication().AddInfrastructure().AddPresentation().BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredKeyedService<ICalculadora>(tipo);

    private static async Task<CalculadoraViewModel> CalcularAsync(TipoCalculadora tipo, ICalculadora calculadora)
    {
        await Banco.Value;
        var notificador = new Notificador();
        var janela = new CalculadoraViewModel(tipo, calculadora, notificador, null!, null!, null!, new ContextoCompartilhado(), new HistoricoDaJanelaFactory(null!, null!, notificador));
        janela.CalcularCommand.Execute(null);
        // O comando não devolve a tarefa do cálculo; ele termina com um resultado, um aviso ou uma mensagem.
        for (var espera = 0; espera < 500 && !janela.TemResultado && !janela.TemAviso && notificador.Mensagens.Count == 0; espera++)
            await Task.Delay(10);
        Assert.Empty(notificador.Mensagens);
        return janela;
    }

    private sealed class Notificador : IUserNotifier
    {
        public List<string> Mensagens { get; } = [];
        public void MostrarAviso(string mensagem, string titulo = "Dados inválidos") => Mensagens.Add(mensagem);
        public void MostrarErro(string mensagem, Exception exception) => throw exception;
        public void MostrarFalha(Erro erro, string contexto) => Mensagens.Add(erro.Mensagem);
        public bool Confirmar(string mensagem, string titulo) => true;
    }
}
