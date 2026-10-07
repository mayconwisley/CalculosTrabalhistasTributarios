using CalculosTrabalhistasTributarios.Presentation;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Presentation.Mvvm;
using CalculosTrabalhistasTributarios.Presentation.ViewModels;
using System.Text.Json;
using Xunit;

namespace CalculosTrabalhistasTributarios.Tests;

/// <summary>Favoritas e busca dos cartões da tela inicial.</summary>
public class PainelAtalhosTests
{
    private sealed class FavoritosEmMemoria(params string[] iniciais) : IFavoritosAtalhos
    {
        private readonly List<string> _chaves = [.. iniciais];
        public IReadOnlyList<string> Chaves => _chaves;
        public void Definir(string chave, bool favorito)
        {
            _chaves.Remove(chave);
            if (favorito) _chaves.Add(chave);
        }
    }

    private static AtalhoViewModel Cartao(string chave, string titulo, string descricao) => new(chave, titulo, descricao, new RelayCommand(_ => { }));

    private static PainelAtalhosViewModel Painel(IFavoritosAtalhos favoritos) => new(
    [
        new("Impostos e salário", [Cartao("Calculadora.Plr", "PLR (participação nos lucros)", "IRRF pela tabela anual exclusiva."), Cartao("Calculadora.CarneLeao", "Carnê-leão", "IR mensal de honorários e aluguéis.")]),
        new("Férias, 13º e desligamento", [Cartao("Calculadora.Rescisao", "Rescisão", "Verbas pelo motivo do desligamento."), Cartao("Calculadora.Ferias", "Férias", "Terço constitucional e venda de dias.")])
    ], favoritos, "calculadora");

    private static string[] Titulos(IEnumerable<AtalhoViewModel> atalhos) => atalhos.Select(atalho => atalho.Titulo).ToArray();

    [Fact]
    public void Favorita_sai_do_grupo_vai_para_a_secao_na_ordem_marcada_e_e_gravada()
    {
        var favoritos = new FavoritosEmMemoria();
        var painel = Painel(favoritos);
        var rescisao = painel.Grupos[1].Todos[0];
        var plr = painel.Grupos[0].Todos[0];

        rescisao.AlternarFavorito!.Execute(null);
        plr.AlternarFavorito!.Execute(null);

        Assert.Equal(["Rescisão", "PLR (participação nos lucros)"], Titulos(painel.Favoritas));
        Assert.Equal(["Calculadora.Rescisao", "Calculadora.Plr"], favoritos.Chaves);
        Assert.Equal(["Carnê-leão"], Titulos(painel.Grupos[0].Atalhos));
        Assert.True(painel.TemFavoritas);
        Assert.Equal("Remover Rescisão das favoritas", rescisao.DescricaoFavorito);

        rescisao.AlternarFavorito.Execute(null);
        Assert.Equal(["Rescisão", "Férias"], Titulos(painel.Grupos[1].Atalhos));
        Assert.Equal(["Calculadora.Plr"], favoritos.Chaves);
    }

    [Fact]
    public void Favoritas_gravadas_voltam_ao_abrir_e_chaves_desconhecidas_sao_ignoradas()
    {
        var painel = Painel(new FavoritosEmMemoria("Calculadora.Ferias", "Calculadora.Removida"));

        Assert.Equal(["Férias"], Titulos(painel.Favoritas));
        Assert.True(painel.Grupos[1].Todos[1].Favorito);
        Assert.Equal(["Rescisão"], Titulos(painel.Grupos[1].Atalhos));
    }

    [Fact]
    public void Busca_ignora_maiusculas_e_acentos_procura_no_titulo_e_na_descricao_e_exige_todas_as_palavras()
    {
        var painel = Painel(new FavoritosEmMemoria("Calculadora.Plr"));

        painel.Filtro = "CARNE";
        Assert.Equal(["Carnê-leão"], Titulos(painel.Grupos.SelectMany(grupo => grupo.Atalhos)));
        Assert.False(painel.TemFavoritas);
        Assert.True(painel.Grupos[1].SemResultado);

        // O nome do grupo não conta: "desligamento" acha a Rescisão pela descrição, não as Férias pelo grupo.
        painel.Filtro = "desligamento";
        Assert.Equal(["Rescisão"], Titulos(painel.Grupos.SelectMany(grupo => grupo.Atalhos)));

        painel.Filtro = "tabela anual";
        Assert.Equal(["PLR (participação nos lucros)"], Titulos(painel.Favoritas));

        painel.Filtro = "terco ferias";
        Assert.Equal(["Férias"], Titulos(painel.Grupos[1].Atalhos));

        painel.Filtro = "inexistente";
        Assert.True(painel.SemResultado);
        Assert.Contains("“inexistente”", painel.MensagemSemResultado);

        painel.Filtro = "";
        Assert.All(painel.Grupos, grupo => Assert.False(grupo.SemResultado));
        Assert.False(painel.SemResultado);
    }

    [Fact]
    public void Grupo_com_todos_os_cartoes_favoritos_continua_na_tela_e_explica()
    {
        var painel = Painel(new FavoritosEmMemoria("Calculadora.Plr", "Calculadora.CarneLeao"));

        Assert.False(painel.Grupos[0].SemResultado);
        Assert.True(painel.Grupos[0].TodosFavoritos);
        Assert.False(painel.Grupos[1].TodosFavoritos);
    }

    [Fact]
    public void Preferencias_antigas_sem_favoritas_e_favoritas_nulas_viram_lista_vazia()
    {
        Assert.Empty(JsonSerializer.Deserialize<Configuracoes>("""{"Theme":"Escuro"}""")!.Favoritos);
        Assert.Empty(JsonSerializer.Deserialize<Configuracoes>("""{"Favoritos":null}""")!.Favoritos);
        Assert.Equal(["Calculadora.Plr"], JsonSerializer.Deserialize<Configuracoes>("""{"Favoritos":["Calculadora.Plr"]}""")!.Favoritos);
    }
}
