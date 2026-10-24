using System.Collections.ObjectModel;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class PaginaLinhasTests
{
    [Fact]
    public void Exibe_doze_linhas_por_pagina_e_navega_ate_a_pagina_parcial()
    {
        var origem = Itens(30);
        var pagina = new PaginaLinhas<string>(origem);

        Assert.Equal(origem.Take(12), pagina.Exibidas);
        Assert.Equal("1–12 de 30", pagina.Texto);
        Assert.True(pagina.TemPaginas);
        Assert.False(pagina.AnteriorCommand.CanExecute(null));

        pagina.ProximaCommand.Execute(null);
        pagina.ProximaCommand.Execute(null);

        Assert.Equal(origem.Skip(24), pagina.Exibidas);
        Assert.Equal("25–30 de 30", pagina.Texto);
        Assert.False(pagina.ProximaCommand.CanExecute(null));
        pagina.AnteriorCommand.Execute(null);
        Assert.Equal("13–24 de 30", pagina.Texto);
    }

    [Fact]
    public void Lista_curta_nao_mostra_navegacao()
    {
        var pagina = new PaginaLinhas<string>(Itens(12));

        Assert.False(pagina.TemPaginas);
        Assert.Equal(12, pagina.Exibidas.Count);
        Assert.Equal(string.Empty, new PaginaLinhas<string>([]).Texto);
    }

    [Fact]
    public void Remover_a_ultima_linha_da_pagina_volta_para_a_anterior()
    {
        var origem = Itens(13);
        var pagina = new PaginaLinhas<string>(origem);
        pagina.ProximaCommand.Execute(null);

        origem.RemoveAt(12);

        Assert.Equal("1–12 de 12", pagina.Texto);
        Assert.Equal(origem, pagina.Exibidas);
    }

    [Fact]
    public void Remover_linha_puxa_a_seguinte_para_a_pagina_e_limpar_esvazia()
    {
        var origem = Itens(20);
        var pagina = new PaginaLinhas<string>(origem);

        origem.RemoveAt(0);
        Assert.Equal(origem.Take(12), pagina.Exibidas);

        origem.Clear();
        Assert.Empty(pagina.Exibidas);
        Assert.Equal(string.Empty, pagina.Texto);
    }

    [Fact]
    public void Exibir_mostra_a_pagina_da_linha()
    {
        var origem = Itens(60);
        var pagina = new PaginaLinhas<string>(origem);

        pagina.Exibir(origem[39]);

        Assert.Equal("37–48 de 60", pagina.Texto);
        Assert.Contains(origem[39], pagina.Exibidas);
    }

    [Fact]
    public void Grade_gera_todas_as_linhas_mas_exibe_uma_pagina_e_mostra_a_linha_adicionada()
    {
        var grade = new GradeMobile("meses", "Meses", "", "Mês", 720, [new("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia)]);
        var competencias = Enumerable.Range(0, 60).Select(i => new DateOnly(2021, 10, 1).AddMonths(i).ToString("MM/yyyy")).ToList();

        grade.Gerar(competencias, "competencia");

        Assert.Equal(60, grade.Linhas.Count);
        Assert.Equal(grade.Linhas.Take(12), grade.Pagina.Exibidas);

        grade.AdicionarCommand.Execute(null);
        Assert.Equal("61 de 61", grade.Pagina.Texto);
        Assert.Same(grade.Linhas[^1], Assert.Single(grade.Pagina.Exibidas));
    }

    private static ObservableCollection<string> Itens(int quantidade) => [.. Enumerable.Range(1, quantidade).Select(i => $"Linha {i}")];
}
