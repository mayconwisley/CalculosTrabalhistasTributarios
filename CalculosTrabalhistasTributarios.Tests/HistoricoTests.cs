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
        var id = await historico.SalvarAsync(null, "Calculadora.Rescisao", "Rescisão", "João da Silva", """{"Campos":{"Salário":"3.000,00"}}""", default);

        var salvo = await historico.ObterAsync(id, default);
        Assert.NotNull(salvo);
        Assert.Equal("João da Silva", salvo.Nome);
        Assert.Equal("Rescisão", salvo.Calculadora);

        // Salvar com o mesmo id substitui o cálculo, sem criar outro.
        Assert.Equal(id, await historico.SalvarAsync(id, "Calculadora.Rescisao", "Rescisão", "João da Silva", """{"Campos":{"Salário":"3.500,00"}}""", default));
        Assert.Contains("3.500,00", (await historico.ObterAsync(id, default))!.Dados);

        var copia = await historico.DuplicarAsync(id, default).Sucesso();
        Assert.NotEqual(id, copia);
        Assert.Equal("João da Silva (cópia)", (await historico.ObterAsync(copia, default))!.Nome);

        await historico.RenomearAsync(copia, "Maria Souza", default);
        var lista = await historico.ListarAsync(default);
        Assert.Equal("Maria Souza", lista.First(calculo => calculo.Id == copia).Nome);

        await historico.ExcluirAsync(id, default);
        await historico.ExcluirAsync(copia, default);
        Assert.Null(await historico.ObterAsync(id, default));

        // Um cálculo excluído enquanto estava aberto volta como novo ao ser salvo.
        var novo = await historico.SalvarAsync(id, "Calculadora.Rescisao", "Rescisão", "João da Silva", "{}", default);
        Assert.NotEqual(id, novo);
        await historico.ExcluirAsync(novo, default);
    }

    [Fact]
    public async Task Historico_filtra_por_calculadora_e_ordena_por_nome()
    {
        var historico = await Ambiente.HistoricoAsync();
        await historico.SalvarAsync(null, "Calculadora.Rescisao", "Rescisão", "Zeta", "{}", default);
        await historico.SalvarAsync(null, "Calculadora.Ferias", "Férias", "Alfa", "{}", default);
        await historico.SalvarAsync(null, "Calculadora.Rescisao", "Rescisão", "Beta", "{}", default);
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
}
