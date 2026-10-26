using CalculosTrabalhistasTributarios.Application;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Infrastructure;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>
/// As janelas próprias do débito judicial, da estabilidade, da jornada e da pensão em atraso mostram os erros de
/// preenchimento como a janela comum: o campo destacado com o que corrigir, o aviso junto do botão e nenhuma caixa de mensagem.
/// </summary>
public class ErrosDasJanelasPropriasTests
{
    [Fact]
    public async Task Estabilidade_aponta_todos_os_campos_errados_e_o_aviso_sai_quando_corrigidos()
    {
        var (tela, notificador) = await CriarAsync<IEstabilidadeViewModelFactory, EstabilidadeViewModel>(fabrica => fabrica.Criar());
        tela.MediaRemuneratoria = "-1";
        tela.Demissao = "31/02/2026";

        await tela.RecalcularAsync();

        Assert.Empty(notificador.Mensagens);
        Assert.False(tela.TemResultado);
        Assert.Equal("Informe zero ou um valor positivo.", tela.Erros[nameof(tela.MediaRemuneratoria)]);
        Assert.Equal("Use uma data dd/mm/aaaa.", tela.Erros[nameof(tela.Demissao)]);
        Assert.Null(tela.Erros[nameof(tela.FimEstabilidade)]);
        Assert.Equal("Corrija os 2 campos destacados: Média remuneratória e Data de demissão.", tela.Erros.Aviso);

        tela.MediaRemuneratoria = "3.000,00";
        Assert.Null(tela.Erros[nameof(tela.MediaRemuneratoria)]);
        Assert.True(tela.Erros.TemAviso);
        tela.Demissao = "01/02/2026";
        Assert.False(tela.Erros.TemAviso);
    }

    [Fact]
    public async Task Estabilidade_destaca_o_campo_citado_pela_regra()
    {
        var (tela, notificador) = await CriarAsync<IEstabilidadeViewModelFactory, EstabilidadeViewModel>(fabrica => fabrica.Criar());
        tela.MediaRemuneratoria = "3.000,00";
        tela.Demissao = "10/03/2026";
        tela.FimEstabilidade = "01/03/2026";

        await tela.RecalcularAsync();

        Assert.Empty(notificador.Mensagens);
        const string mensagem = "O fim da estabilidade deve ser posterior à data de demissão.";
        Assert.Equal(mensagem, tela.Erros[nameof(tela.FimEstabilidade)]);
        Assert.Null(tela.Erros[nameof(tela.Demissao)]);
        Assert.Equal(mensagem, tela.Erros.Aviso);
    }

    [Fact]
    public async Task Estabilidade_sem_media_pede_a_media()
    {
        var (tela, notificador) = await CriarAsync<IEstabilidadeViewModelFactory, EstabilidadeViewModel>(fabrica => fabrica.Criar());

        await tela.RecalcularAsync();

        Assert.Empty(notificador.Mensagens);
        Assert.Equal("Informe a média remuneratória, maior que zero.", tela.Erros[nameof(tela.MediaRemuneratoria)]);
    }

    [Fact]
    public async Task Debito_aponta_o_campo_e_a_linha_da_grade()
    {
        var (tela, notificador) = await CriarAsync<IDebitoJudicialViewModelFactory, DebitoJudicialViewModel>(fabrica => fabrica.Criar());
        tela.DataCalculo = "";

        await tela.RecalcularAsync();

        Assert.Empty(notificador.Mensagens);
        Assert.Equal("Use uma data dd/mm/aaaa.", tela.Erros[nameof(tela.DataCalculo)]);
        Assert.Equal("Informe o vencimento da parcela 1 no formato dd/mm/aaaa.", tela.Erros[nameof(tela.Parcelas)]);
        Assert.Equal("Corrija os 2 campos destacados: Data do cálculo e Parcelas.", tela.Erros.Aviso);

        tela.Parcelas[0].Vencimento = "10/01/2025";
        Assert.Null(tela.Erros[nameof(tela.Parcelas)]);
        Assert.NotNull(tela.Erros[nameof(tela.DataCalculo)]);
    }

    [Fact]
    public async Task Debito_destaca_o_ajuizamento_posterior_ao_calculo()
    {
        var (tela, notificador) = await CriarAsync<IDebitoJudicialViewModelFactory, DebitoJudicialViewModel>(fabrica => fabrica.Criar());
        tela.Parcelas[0].Vencimento = "10/01/2025";
        tela.Parcelas[0].Valor = "1.000,00";
        tela.DataCalculo = "01/02/2025";
        tela.DataAjuizamento = "01/03/2025";

        await tela.RecalcularAsync();

        Assert.Empty(notificador.Mensagens);
        Assert.Equal("A data do ajuizamento não pode ser posterior à data do cálculo.", tela.Erros[nameof(tela.DataAjuizamento)]);
        Assert.Null(tela.Erros[nameof(tela.DataCalculo)]);
    }

    [Fact]
    public async Task Jornada_aponta_os_campos_do_horario_padrao_antes_de_gerar_os_dias()
    {
        var (tela, notificador) = await CriarAsync<IJornadaViewModelFactory, JornadaViewModel>(fabrica => fabrica.Criar());
        tela.JornadaSemana = "oito";
        tela.Entrada1 = "25:00";

        tela.GerarDiasCommand.Execute(null);

        Assert.Empty(notificador.Mensagens);
        Assert.Empty(tela.Dias);
        Assert.Equal("Use horas e minutos, como 8:00.", tela.Erros[nameof(tela.JornadaSemana)]);
        Assert.Equal("Use um horário, como 08:00.", tela.Erros[nameof(tela.Entrada1)]);
        Assert.Equal("Corrija os 2 campos destacados: Jornada de seg. a sex. e Entrada.", tela.Erros.Aviso);
    }

    [Fact]
    public async Task Jornada_aponta_a_grade_com_a_marcacao_errada()
    {
        var (tela, notificador) = await CriarAsync<IJornadaViewModelFactory, JornadaViewModel>(fabrica => fabrica.Criar());
        tela.GerarDiasCommand.Execute(null);
        var dia = tela.Dias.First(item => item.Entrada1.Length > 0);
        dia.Saida1 = "";

        tela.ApurarCommand.Execute(null);

        Assert.Empty(notificador.Mensagens);
        Assert.False(tela.TemResultado);
        Assert.Equal($"Em {dia.Dia}, informe a entrada e a saída como horários, por exemplo 08:00 e 12:00.", tela.Erros[nameof(tela.Dias)]);
        Assert.Equal(tela.Erros[nameof(tela.Dias)], tela.Erros.Aviso);

        dia.Saida1 = "12:00";
        Assert.Null(tela.Erros[nameof(tela.Dias)]);
        Assert.False(tela.Erros.TemAviso);
    }

    [Fact]
    public async Task Pensao_em_atraso_aponta_so_os_campos_a_vista()
    {
        var (tela, notificador) = await CriarAsync<IPensaoAtrasoViewModelFactory, PensaoAtrasoViewModel>(fabrica => fabrica.Criar());
        tela.ValorPensao = "-5";
        tela.Percentual = "abc";
        tela.UltimaParcela = "13/2025";

        await tela.RecalcularAsync();

        Assert.Empty(notificador.Mensagens);
        Assert.Equal("Informe zero ou um valor positivo.", tela.Erros[nameof(tela.ValorPensao)]);
        Assert.Equal("Use o mês e o ano, mm/aaaa.", tela.Erros[nameof(tela.UltimaParcela)]);
        // Com o valor fixo, o percentual do salário mínimo fica oculto e não é lido.
        Assert.Null(tela.Erros[nameof(tela.Percentual)]);
        Assert.Equal("Corrija os 2 campos destacados: Valor mensal e Última parcela.", tela.Erros.Aviso);
    }

    [Theory]
    [InlineData("01/2025", "12/2024", nameof(PensaoAtrasoViewModel.UltimaParcela), "A última parcela deve ser igual ou posterior à primeira.")]
    [InlineData("01/2000", "12/2025", nameof(PensaoAtrasoViewModel.UltimaParcela), "Informe um mês até 20 anos depois da primeira parcela, no máximo 240 parcelas.")]
    public async Task Pensao_em_atraso_destaca_o_campo_citado_pela_regra(string primeira, string ultima, string campo, string orientacao)
    {
        var (tela, notificador) = await CriarAsync<IPensaoAtrasoViewModelFactory, PensaoAtrasoViewModel>(fabrica => fabrica.Criar());
        tela.ValorPensao = "500,00";
        tela.PrimeiraParcela = primeira;
        tela.UltimaParcela = ultima;

        await tela.RecalcularAsync();

        Assert.Empty(notificador.Mensagens);
        Assert.Equal(orientacao, tela.Erros[campo]);
        Assert.Null(tela.Erros[nameof(tela.PrimeiraParcela)]);
        Assert.True(tela.Erros.TemAviso);
    }

    private static async Task<(TTela Tela, Notificador Notificador)> CriarAsync<TFabrica, TTela>(Func<TFabrica, TTela> criar) where TFabrica : notnull
    {
        await Ambiente.ConsultaAsync();
        var notificador = new Notificador();
        var servicos = new ServiceCollection().AddApplication().AddInfrastructure().AddPresentation().AddSingleton<IUserNotifier>(notificador)
            .BuildServiceProvider().CreateScope().ServiceProvider;
        return (criar(servicos.GetRequiredService<TFabrica>()), notificador);
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
