using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.Behaviors;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Services;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;
using CalculosTrabalhistasTributarios.Presentation.ViewModels.Historico;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Recentes, comparação de resultados, ajuda pelo manual, posição das janelas e cópia de valores.</summary>
public class UsabilidadeTests
{
    [Fact]
    public void Janela_maior_que_a_tela_com_escala_cabe_na_area_util()
    {
        // 1366 x 768 com 125%: 1092,8 x 614,4 unidades, menos a barra de tarefas (cerca de 38): área de 1092,8 x 576.
        var area = new Rect(0, 0, 1092.8, 576);
        var (minimo, janela) = JanelaLembrada.Encaixar(area, new Size(900, 620), new Rect(40, 10, 1180, 840));

        Assert.Equal(new Size(900, 576), minimo);
        Assert.Equal(new Rect(0, 0, 1092.8, 576), janela);
    }

    [Fact]
    public void Janela_que_cabe_so_volta_para_dentro_da_tela()
    {
        var area = new Rect(0, 0, 1920, 1040);
        var (minimo, janela) = JanelaLembrada.Encaixar(area, new Size(900, 620), new Rect(1500, -30, 1180, 840));

        Assert.Equal(new Size(900, 620), minimo);
        Assert.Equal(new Rect(740, 0, 1180, 840), janela);
        // Sem posição definida, a janela continua sendo centralizada sobre a que a abriu.
        Assert.True(double.IsNaN(JanelaLembrada.Encaixar(area, new Size(900, 620), new Rect(double.NaN, double.NaN, 1180, 840)).Janela.X));
    }

    [Theory]
    [InlineData("R$ 1.234,56", "1.234,56")]
    [InlineData("-R$ 10,00", "-10,00")]
    [InlineData("+R$ 5,00", "5,00")]
    [InlineData("R$ 0,00", "0,00")]
    [InlineData("27,5%", "27,5%")]
    [InlineData("12/12", "12/12")]
    public void Valor_copiado_sai_pronto_para_colar(string exibido, string copiado) => Assert.Equal(copiado, CopiaDeValor.Texto(exibido));

    [Fact]
    public void Toda_janela_de_calculadora_tem_secao_no_manual()
    {
        var titulos = File.ReadAllLines(Path.Combine(Raiz(), "docs", "MANUAL.md"))
            .Select(linha => Regex.Match(linha, @"^#{2,4} (\d+(?:\.\d+)*)\.? ")).Where(titulo => titulo.Success).Select(titulo => titulo.Groups[1].Value).ToHashSet();

        Assert.DoesNotContain(Enum.GetValues<TipoCalculadora>(), tipo => SecoesDoManual.Numero($"Calculadora.{tipo}") is null);
        Assert.DoesNotContain(SecoesDoManual.Chaves, chave => SecoesDoManual.Numero(chave) is not { } numero || !titulos.Contains(numero));
        Assert.Equal("9", SecoesDoManual.Numero("Tabela.Inss"));
        Assert.Null(SecoesDoManual.Numero("Principal"));
    }

    [Fact]
    public void Toda_tela_de_calculo_salvavel_atende_aos_atalhos_de_teclado()
    {
        // A janela principal liga Ctrl+Enter, Ctrl+S, Ctrl+P e Ctrl+E pela interface; uma tela sem ela ficaria sem atalhos.
        var telas = typeof(ICalculoSalvavel).Assembly.GetTypes()
            .Where(tipo => tipo is { IsClass: true, IsAbstract: false } && typeof(ICalculoSalvavel).IsAssignableFrom(tipo)).ToArray();

        Assert.NotEmpty(telas);
        Assert.DoesNotContain(telas, tipo => !typeof(IAtalhosDeCalculo).IsAssignableFrom(tipo));
        Assert.Contains(AtalhosDeTeclado.Lista, atalho => atalho.Teclas == "Ctrl+Enter");
    }

    [Fact]
    public void Pdf_do_manual_tem_a_pagina_de_cada_secao()
    {
        var indice = IndiceManualPdf.Ler(File.ReadAllBytes(Path.Combine(Raiz(), "docs", "ManualDoUsuario.pdf")));

        var paginas = SecoesDoManual.Chaves.ToDictionary(chave => chave, chave => IndiceManualPdf.Pagina(indice, SecoesDoManual.Numero(chave)!));
        Assert.Empty(paginas.Where(pagina => pagina.Value is null).Select(pagina => pagina.Key));
        // A ordem das páginas segue a do manual; "8.1" não confunde com "8.15", nem "8.23" com "8.23.1".
        Assert.True(paginas["Calculadora.SalarioPeloLiquido"] < paginas["Jornada"]);
        Assert.True(paginas["Calculadora.Holerite"] <= paginas["Jornada"]);
        Assert.True(paginas["Calculadora.CarneLeao"] <= paginas["Calculadora.TrabalhoExterior"]);
        Assert.True(paginas["Calculadora.SimulacaoTributaria"] > 2);
    }

    [Fact]
    public void Comparacao_de_resultados_mostra_a_diferenca_de_cada_verba()
    {
        var primeiro = Demonstrativo([new("Salário", "30 dias", 3000m)], [new("INSS", "", 234.56m)], "R$ 2.765,44");
        var segundo = Demonstrativo([new("Salário", "30 dias", 3500m), new("Horas extras", "10:00", 238.64m)], [new("INSS", "", 290.31m), new("IRRF", "", 12.34m)], "R$ 3.435,99");

        var linhas = ComparacaoResultados.Linhas(primeiro, segundo);

        Assert.Contains(new LinhaResultadoComparadoViewModel("Resumo", "Líquido", "R$ 2.765,44", "R$ 3.435,99", "+R$ 670,55"), Normalizadas(linhas));
        Assert.Contains(new LinhaResultadoComparadoViewModel("Proventos", "Salário", "R$ 3.000,00", "R$ 3.500,00", "+R$ 500,00"), Normalizadas(linhas));
        Assert.Contains(new LinhaResultadoComparadoViewModel("Proventos", "Horas extras", "—", "R$ 238,64", "+R$ 238,64"), Normalizadas(linhas));
        Assert.Contains(new LinhaResultadoComparadoViewModel("Descontos", "IRRF", "—", "R$ 12,34", "+R$ 12,34"), Normalizadas(linhas));
        // 3.000,00 - 234,56 = 2.765,44; 3.738,64 - 302,65 = 3.435,99.
        Assert.Contains(new LinhaResultadoComparadoViewModel("Totais", "Líquido a receber", "R$ 2.765,44", "R$ 3.435,99", "+R$ 670,55"), Normalizadas(linhas));
        Assert.Equal(-10m, ComparacaoResultados.LerMoeda("-R$ 10,00"));
        Assert.Null(ComparacaoResultados.LerMoeda("27,5%"));
    }

    [Fact]
    public void Comparacao_pareia_linhas_com_chave_pela_posicao_mesmo_com_nomes_diferentes()
    {
        var primeiro = Demonstrativo(
            [new("1º vínculo: Empresa C", "", 5000m) { ChaveComparacao = "1º vínculo" }, new("2º vínculo: Empresa D", "", 2000m) { ChaveComparacao = "2º vínculo" }],
            [new("INSS", "", 10m), new("Pensão - FilhoB", "", 100m) { ChaveComparacao = "Pensão do 1º beneficiário" }], "R$ 6.890,00");
        var segundo = Demonstrativo(
            [new("1º vínculo: Empresa A", "", 4500m) { ChaveComparacao = "1º vínculo" }, new("2º vínculo: Empresa B", "", 3000m) { ChaveComparacao = "2º vínculo" },
             new("3º vínculo: Empresa E", "", 1000m) { ChaveComparacao = "3º vínculo" }],
            [new("INSS", "", 10m), new("Pensão - Ana", "", 150m) { ChaveComparacao = "Pensão do 1º beneficiário" }], "R$ 8.340,00");

        var linhas = Normalizadas(ComparacaoResultados.Linhas(primeiro, segundo)).Where(linha => linha.Grupo is "Proventos" or "Descontos");

        Assert.Equal(
        [
            new("Proventos", "1º vínculo: Empresa C → Empresa A", "R$ 5.000,00", "R$ 4.500,00", "-R$ 500,00"),
            new("Proventos", "2º vínculo: Empresa D → Empresa B", "R$ 2.000,00", "R$ 3.000,00", "+R$ 1.000,00"),
            new("Proventos", "3º vínculo: Empresa E", "—", "R$ 1.000,00", "+R$ 1.000,00"),
            new("Descontos", "INSS", "R$ 10,00", "R$ 10,00", "sem diferença"),
            new LinhaResultadoComparadoViewModel("Descontos", "Pensão - FilhoB → Ana", "R$ 100,00", "R$ 150,00", "+R$ 50,00")
        ], linhas);
    }

    [Fact]
    public void Comparacao_sem_diferenca_e_cenario_que_nao_recalcula()
    {
        var demonstrativo = Demonstrativo([new("Salário", "", 3000m)], [], "R$ 3.000,00");
        var salvo = new CalculoSalvoDto(1, "Calculadora.Holerite", "Holerite", "A", new DadosFormulario(new() { ["Salário"] = "3.000,00" }).ParaJson(), DateTime.Today, DateTime.Today);

        var comparacao = new ComparacaoHistoricoViewModel(salvo, salvo with { Id = 2, Nome = "B" }, demonstrativo, demonstrativo);
        Assert.All(comparacao.LinhasResultado, linha => Assert.False(linha.Diferente));
        comparacao.SomenteDiferencas = true;
        Assert.Empty(comparacao.LinhasResultado);

        var falha = new ComparacaoHistoricoViewModel(salvo, salvo with { Id = 2, Nome = "B" }, demonstrativo, Erro.Validacao("Salário: informe um valor."));
        Assert.False(falha.TemResultados);
        Assert.Equal("Não foi possível recalcular “B”: Salário: informe um valor.", falha.MensagemResultados);

        // Cenários da mesma janela não são refeitos: a explicação diz de quando é o A.
        var cenarios = new ComparacaoHistoricoViewModel(salvo with { CriadoEm = new DateTime(2026, 10, 20, 9, 5, 0) }, salvo with { Id = 0, Nome = "B" }, demonstrativo, demonstrativo, cenarios: true);
        Assert.Equal("Comparação de cenários", cenarios.Cabecalho);
        Assert.StartsWith("O cenário A foi calculado às 09:05;", cenarios.MensagemResultados);
        Assert.Equal("Comparação de cálculos salvos", comparacao.Cabecalho);
    }

    [Theory]
    [InlineData("2026-10-13 14:32", "hoje às 14:32")]
    [InlineData("2026-10-12 09:05", "ontem às 09:05")]
    [InlineData("2026-09-30 18:00", "30/09/2026")]
    public void Recente_mostra_quando_foi_alterado(string alteradoEm, string esperado) =>
        Assert.Equal(esperado, ItemHistoricoViewModel.Descrever(DateTime.Parse(alteradoEm, System.Globalization.CultureInfo.InvariantCulture), new DateTime(2026, 10, 13)));

    [Fact]
    public async Task Recentes_sao_os_cinco_ultimos_alterados_sem_mexer_na_lista()
    {
        var historico = new HistoricoFalso(Enumerable.Range(1, 7).Select(dia =>
            new CalculoSalvoDto(dia, "Calculadora.Ferias", "Férias", $"Cálculo {dia}", "{}", new DateTime(2026, 10, dia), new DateTime(2026, 10, dia))).ToArray());
        var tela = new HistoricoViewModel(historico, null!, null!, null!, null!);

        await tela.AtualizarRecentesAsync();

        Assert.Equal(["Cálculo 7", "Cálculo 6", "Cálculo 5", "Cálculo 4", "Cálculo 3"], tela.Recentes.Select(item => item.Nome));
        Assert.True(tela.TemRecentes);
        Assert.Empty(tela.Itens);
    }

    private static DemonstrativoDto Demonstrativo(VerbaDto[] proventos, VerbaDto[] descontos, string liquido) =>
        new("Holerite", "10/2026", [new DestaqueDto("Líquido", liquido, "")], proventos, descontos, [], [], []);

    private static LinhaResultadoComparadoViewModel[] Normalizadas(IEnumerable<LinhaResultadoComparadoViewModel> linhas) =>
        linhas.Select(linha => linha with { Primeiro = Texto.Normalizar(linha.Primeiro), Segundo = Texto.Normalizar(linha.Segundo), Diferenca = Texto.Normalizar(linha.Diferenca) }).ToArray();

    /// <summary>A raiz do repositório, acima da pasta de saída dos testes.</summary>
    private static string Raiz()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
            if (File.Exists(Path.Combine(pasta.FullName, "docs", "MANUAL.md")))
                return pasta.FullName;
        throw new DirectoryNotFoundException("Não foi encontrada a pasta docs do repositório.");
    }

    private sealed class HistoricoFalso(IReadOnlyList<CalculoSalvoDto> calculos) : IHistoricoCalculos
    {
        public Task<IReadOnlyList<CalculoSalvoDto>> ListarAsync(CancellationToken cancellationToken) => Task.FromResult(calculos);
        public Task<CalculoSalvoDto?> ObterAsync(long id, CancellationToken cancellationToken) => Task.FromResult(calculos.FirstOrDefault(calculo => calculo.Id == id));
        public Task<long> SalvarAsync(long? id, string tipo, string calculadora, string nome, string dados, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<long>> DuplicarAsync(long id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RenomearAsync(long id, string nome, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ExcluirAsync(long id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
