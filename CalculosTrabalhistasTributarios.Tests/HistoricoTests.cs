using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

public class HistoricoTests
{
    [Fact]
    public void Comparacao_mostra_diferencas_em_campos_e_linhas_salvas()
    {
        var primeiro = new CalculoSalvoDto(1, "Calculadora.Rescisao", "Rescisão", "Cenário A",
            new DadosFormulario(new() { ["Salário"] = "3.000,00", ["Depósitos"] = "[{\"Competencia\":\"01/2026\",\"Valor\":\"240,00\"}]" }).ParaJson(), DateTime.Today, DateTime.Today);
        var segundo = primeiro with { Id = 2, Nome = "Cenário B",
            Dados = new DadosFormulario(new() { ["Salário"] = "3.500,00", ["Depósitos"] = "[{\"Competencia\":\"01/2026\",\"Valor\":\"280,00\"}]" }).ParaJson() };

        var comparacao = new ComparacaoHistoricoViewModel(primeiro, segundo);

        Assert.Contains(comparacao.Linhas, linha => linha.Campo == "Salário" && linha.Primeiro == "3.000,00" && linha.Segundo == "3.500,00");
        Assert.Contains(comparacao.Linhas, linha => linha.Campo == "Depósitos · linha 1 · Valor" && linha.Diferente);
        comparacao.SomenteDiferencas = true;
        Assert.DoesNotContain(comparacao.Linhas, linha => linha.Campo == "Depósitos · linha 1 · Competencia");
    }

    [Fact]
    public async Task Salva_altera_duplica_renomeia_e_exclui()
    {
        var historico = await Ambiente.HistoricoAsync();
        var id = await historico.SalvarAsync(null, "Calculadora.Rescisao", "Rescisão", "João da Silva", """{"Campos":{"Salário":"3.000,00"}}""", TestContext.Current.CancellationToken);

        var salvo = await historico.ObterAsync(id, TestContext.Current.CancellationToken);
        Assert.NotNull(salvo);
        Assert.Equal("João da Silva", salvo.Nome);
        Assert.Equal("Rescisão", salvo.Calculadora);

        // Salvar com o mesmo id substitui o cálculo, sem criar outro.
        Assert.Equal(id, await historico.SalvarAsync(id, "Calculadora.Rescisao", "Rescisão", "João da Silva", """{"Campos":{"Salário":"3.500,00"}}""", TestContext.Current.CancellationToken));
        Assert.Contains("3.500,00", (await historico.ObterAsync(id, TestContext.Current.CancellationToken))!.Dados);

        var copia = await historico.DuplicarAsync(id, TestContext.Current.CancellationToken).Sucesso();
        Assert.NotEqual(id, copia);
        Assert.Equal("João da Silva (cópia)", (await historico.ObterAsync(copia, TestContext.Current.CancellationToken))!.Nome);

        await historico.RenomearAsync(copia, "Maria Souza", TestContext.Current.CancellationToken);
        var lista = await historico.ListarAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Maria Souza", lista.First(calculo => calculo.Id == copia).Nome);

        await historico.ExcluirAsync(id, TestContext.Current.CancellationToken);
        await historico.ExcluirAsync(copia, TestContext.Current.CancellationToken);
        Assert.Null(await historico.ObterAsync(id, TestContext.Current.CancellationToken));

        // Um cálculo excluído enquanto estava aberto volta como novo ao ser salvo.
        var novo = await historico.SalvarAsync(id, "Calculadora.Rescisao", "Rescisão", "João da Silva", "{}", TestContext.Current.CancellationToken);
        Assert.NotEqual(id, novo);
        await historico.ExcluirAsync(novo, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Historico_filtra_por_calculadora_e_ordena_por_nome()
    {
        var historico = await Ambiente.HistoricoAsync();
        await historico.SalvarAsync(null, "Calculadora.Rescisao", "Rescisão", "Zeta", "{}", TestContext.Current.CancellationToken);
        await historico.SalvarAsync(null, "Calculadora.Ferias", "Férias", "Alfa", "{}", TestContext.Current.CancellationToken);
        await historico.SalvarAsync(null, "Calculadora.Rescisao", "Rescisão", "Beta", "{}", TestContext.Current.CancellationToken);
        var tela = new HistoricoViewModel(historico, null!, null!, null!, null!);

        await tela.CarregarAsync();
        tela.CalculadoraSelecionada = "Rescisão";
        tela.OrdemSelecionada = "Nome";

        Assert.Equal(["Beta", "Zeta"], tela.Itens.Select(item => item.Nome));
        tela.Itens[0].Selecionado = true;
        tela.Itens[1].Selecionado = true;
        Assert.True(tela.CompararCommand.CanExecute(null));
        tela.PeriodoSelecionado = "Este ano";
        Assert.Equal(2, tela.Itens.Count);
        Assert.False(tela.CompararCommand.CanExecute(null));
        tela.Filtro = "zeta";
        Assert.Single(tela.Itens);
    }

    /// <summary>Todas as calculadoras da janela padrão, com os simuladores nulos: só o formulário é usado.</summary>
    public static TheoryData<Type> Calculadoras()
    {
        var dados = new TheoryData<Type>();
        foreach (var tipo in typeof(CalculadoraBase).Assembly.GetTypes().Where(tipo => !tipo.IsAbstract && typeof(ICalculadora).IsAssignableFrom(tipo)))
            dados.Add(tipo);
        return dados;
    }

    private static ICalculadora Criar(Type tipo) =>
        (ICalculadora)Activator.CreateInstance(tipo, tipo.GetConstructors()[0].GetParameters().Select(_ => (object?)null).ToArray())!;

    [Theory]
    [MemberData(nameof(Calculadoras))]
    public void Formulario_volta_igual_ao_reabrir(Type tipo)
    {
        var original = Criar(tipo);
        // Muda cada campo: a última opção de cada lista e um valor diferente do inicial em cada texto.
        foreach (var campo in original.Campos)
        {
            if (campo is CampoOpcaoViewModel opcao)
                opcao.Selecionada = opcao.Opcoes[^1];
        }
        foreach (var campo in original.Campos.OfType<CampoTextoViewModel>())
            campo.Valor = $"valor de {campo.Rotulo}";

        // Os rótulos são as chaves do histórico: um rótulo repetido faria um campo apagar o outro.
        var campos = original.ExportarCampos();
        Assert.Equal(original.Campos.Count, campos.Count);

        var dados = DadosFormulario.DeJson(new DadosFormulario(campos).ParaJson());
        var reaberto = Criar(tipo);
        reaberto.ImportarCampos(dados.Campos);
        Assert.Equal(campos, reaberto.ExportarCampos());
    }

    [Fact]
    public void Holerite_salvo_com_os_rotulos_antigos_reabre_nos_campos_novos()
    {
        var holerite = Criar(typeof(CalculadoraHolerite));
        holerite.ImportarCampos(new Dictionary<string, string> { ["Outros proventos"] = "500,00", ["Dependentes"] = "2", ["Outros descontos"] = "80,00" });

        var campos = holerite.ExportarCampos();
        Assert.Equal("500,00", campos["Proventos tributáveis"]);
        Assert.Equal("2", campos["Dependentes (IRRF)"]);
        Assert.Equal("80,00", campos["Descontos sem incidência"]);
        Assert.Equal(2, holerite.Contexto.Dependentes);
    }

    [Theory]
    [InlineData(typeof(CalculadoraAposentadoria), "Sexo", "Mulher")]
    [InlineData(typeof(CalculadoraPensaoMorte), "Situação do segurado", "Não aposentado")]
    public void Media_digitada_no_historico_antigo_reabre_como_media_informada(Type tipo, string outroCampo, string outroValor)
    {
        // Antes do cálculo pelos salários, "Média dos salários" guardava o valor digitado da média.
        var antiga = Criar(tipo);
        antiga.ImportarCampos(new Dictionary<string, string> { [outroCampo] = outroValor, ["Média dos salários"] = "5.000,00" });

        var campos = antiga.ExportarCampos();
        Assert.Equal(("Informar a média pronta", "5.000,00", "[]"), (campos["Média dos salários"], campos["Média informada"], campos["Salários de contribuição"]));
        Assert.Equal(outroValor, campos[outroCampo]);
    }

    [Theory]
    [InlineData(typeof(CalculadoraAposentadoria))]
    [InlineData(typeof(CalculadoraPensaoMorte))]
    public void Periodos_de_salario_reabrem_na_ordem(Type tipo)
    {
        var original = Criar(tipo);
        var salarios = original.Campos.OfType<CampoSalariosContribuicaoViewModel>().Single();
        salarios.AdicionarCommand.Execute(null);
        salarios.Linhas[0].Fim = "12/1999";
        salarios.Linhas[0].Salario = "800,00";
        salarios.AdicionarCommand.Execute(null);
        Assert.Equal("01/2000", salarios.Linhas[1].Inicio);   // começa no mês seguinte ao fim do período anterior
        salarios.Linhas[1].Fim = "09/2026";
        salarios.Linhas[1].Salario = "3.500,00";

        var reaberto = Criar(tipo);
        reaberto.ImportarCampos(DadosFormulario.DeJson(new DadosFormulario(original.ExportarCampos()).ParaJson()).Campos);
        var linhas = reaberto.Campos.OfType<CampoSalariosContribuicaoViewModel>().Single().Linhas;
        Assert.Equal([("07/1994", "12/1999", "800,00"), ("01/2000", "09/2026", "3.500,00")], linhas.Select(linha => (linha.Inicio, linha.Fim, linha.Salario)));
    }
}
