using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class GradeAcoesLinhaTests
{
    private static GradeMobile Criar(int limite = 5, params string[] nomes)
    {
        var grade = new GradeMobile("vinculos", "Vínculos", "", "Vínculo", limite,
            [new("nome", "Nome", TipoCampoMobile.Texto), new("tipo", "Tipo", TipoCampoMobile.Opcao, Opcoes: ["A", "B"]), new("ativo", "Ativo", TipoCampoMobile.SimNao)])
        { Reordenavel = true };
        foreach (var nome in nomes) grade.AdicionarLinha(new Dictionary<string, string> { ["nome"] = nome });
        return grade;
    }

    private static string[] Nomes(GradeMobile grade) => [.. grade.Linhas.Select(linha => linha.Campo("nome").Valor)];

    [Fact]
    public void Subir_e_descer_mudam_a_ordem_renumeram_e_respeitam_as_pontas()
    {
        var grade = Criar(5, "A", "B", "C");
        var alteracoes = 0;
        grade.Alterada += (_, _) => alteracoes++;

        Assert.False(grade.SubirCommand.CanExecute(grade.Linhas[0]));
        Assert.False(grade.DescerCommand.CanExecute(grade.Linhas[2]));
        grade.SubirCommand.Execute(grade.Linhas[2]);
        grade.DescerCommand.Execute(grade.Linhas[0]);

        Assert.Equal(["C", "A", "B"], Nomes(grade));
        Assert.Equal(["Vínculo 1", "Vínculo 2", "Vínculo 3"], grade.Linhas.Select(linha => linha.Titulo));
        Assert.Equal(2, alteracoes);
    }

    [Fact]
    public void Linha_movida_continua_avisando_alteracoes_dos_campos()
    {
        var grade = Criar(5, "A", "B");
        grade.DescerCommand.Execute(grade.Linhas[0]);
        var alteracoes = 0;
        grade.Alterada += (_, _) => alteracoes++;

        grade.Linhas[1].Campo("nome").Valor = "A2";

        Assert.Equal(1, alteracoes);
    }

    [Fact]
    public void Duplicar_copia_texto_opcao_e_marcacao_logo_abaixo_e_respeita_o_limite()
    {
        var grade = Criar(3, "A", "B");
        grade.Linhas[0].Campo("tipo").Indice = 1;
        grade.Linhas[0].Campo("ativo").Marcado = true;

        grade.DuplicarCommand.Execute(grade.Linhas[0]);

        Assert.Equal(["A", "A", "B"], Nomes(grade));
        var copia = grade.Linhas[1];
        Assert.NotSame(grade.Linhas[0], copia);
        Assert.Equal(1, copia.Campo("tipo").Indice);
        Assert.True(copia.Campo("ativo").Marcado);
        copia.Campo("nome").Valor = "Cópia";
        Assert.Equal("A", grade.Linhas[0].Campo("nome").Valor);
        Assert.False(grade.DuplicarCommand.CanExecute(grade.Linhas[0]));
    }

    [Fact]
    public void Desfazer_devolve_a_linha_com_os_valores_na_mesma_posicao()
    {
        var grade = Criar(5, "A", "B", "C");
        var removida = grade.Linhas[1];

        grade.RemoverCommand.Execute(removida);

        Assert.Equal(["A", "C"], Nomes(grade));
        Assert.Equal("Linha removida: Vínculo 2 (B).", grade.Remocao.Texto);
        Assert.True(grade.Remocao.DesfazerCommand.CanExecute(null));
        grade.Remocao.DesfazerCommand.Execute(null);
        Assert.Equal(["A", "B", "C"], Nomes(grade));
        Assert.Same(removida, grade.Linhas[1]);
        Assert.False(grade.Remocao.Visivel);
        Assert.False(grade.Remocao.DesfazerCommand.CanExecute(null));
    }

    [Fact]
    public void Remocao_sem_nome_identifica_a_linha_pela_posicao()
    {
        var grade = Criar(5, "A", " ");

        grade.RemoverCommand.Execute(grade.Linhas[1]);

        Assert.Equal("Linha removida: Vínculo 2.", grade.Remocao.Texto);
    }

    [Fact]
    public void Desfazer_alcanca_so_a_ultima_remocao_e_nao_passa_do_limite()
    {
        var grade = Criar(3, "A", "B", "C");
        grade.RemoverCommand.Execute(grade.Linhas[0]);
        grade.RemoverCommand.Execute(grade.Linhas[0]);
        grade.AdicionarLinha(new Dictionary<string, string> { ["nome"] = "D" });
        grade.AdicionarLinha(new Dictionary<string, string> { ["nome"] = "E" });

        grade.Remocao.DesfazerCommand.Execute(null);

        Assert.Equal(["C", "D", "E"], Nomes(grade));
        Assert.Contains("limite de 3 linhas", grade.Remocao.Texto);
        Assert.False(grade.Remocao.PodeDesfazer);
    }

    [Fact]
    public void Gerar_meses_descarta_o_desfazer_pendente()
    {
        var grade = Criar(5, "01/2026", "02/2026");
        grade.RemoverCommand.Execute(grade.Linhas[1]);

        grade.Gerar(["01/2026", "02/2026", "03/2026"], "nome");

        Assert.False(grade.Remocao.Visivel);
        Assert.Equal(["01/2026", "02/2026", "03/2026"], Nomes(grade));
    }

    [Fact]
    public void Importar_historico_descarta_o_desfazer_das_grades()
    {
        var calculadora = new InssMultiplosVinculosMobileViewModel(new SimulacaoNaoUsada(), new PdfNaoUsado(), NullLogger<InssMultiplosVinculosMobileViewModel>.Instance);
        var vinculos = Assert.Single(calculadora.Grades);
        Assert.True(vinculos.Reordenavel);
        var dados = calculadora.ExportarDados();
        vinculos.RemoverCommand.Execute(vinculos.Linhas[0]);

        Assert.True(calculadora.TentarImportarDados(dados).Sucesso);

        Assert.False(vinculos.Remocao.Visivel);
        Assert.Equal(2, vinculos.Linhas.Count);
    }

    [Fact]
    public void Rodape_mostra_so_a_ultima_remocao_do_formulario()
    {
        var calculadora = new InssMultiplosVinculosMobileViewModel(new SimulacaoNaoUsada(), new PdfNaoUsado(), NullLogger<InssMultiplosVinculosMobileViewModel>.Instance);
        var vinculos = Assert.Single(calculadora.Grades);
        Assert.False(calculadora.TemRemocaoAtual);

        vinculos.RemoverCommand.Execute(vinculos.Linhas[0]);
        Assert.Same(vinculos.Remocao, calculadora.RemocaoAtual);

        var lista = new System.Collections.ObjectModel.ObservableCollection<string>(["x"]);
        calculadora.RemocaoLinhas.Remover(lista, "x", "Item", 5);
        Assert.Same(calculadora.RemocaoLinhas, calculadora.RemocaoAtual);
        Assert.False(vinculos.Remocao.Visivel);

        calculadora.RemocaoLinhas.DesfazerCommand.Execute(null);
        Assert.Equal(["x"], lista);
        Assert.Null(calculadora.RemocaoAtual);
    }

    private sealed class SimulacaoNaoUsada : CalculosTrabalhistasTributarios.Application.Interfaces.ISimularDemonstrativoUseCase<CalculosTrabalhistasTributarios.Application.DTOs.SimularMultiplosVinculosInssRequest>
    {
        public Task<CalculosTrabalhistasTributarios.Domain.Comum.Result<CalculosTrabalhistasTributarios.Application.DTOs.DemonstrativoDto>> ExecutarAsync(
            CalculosTrabalhistasTributarios.Application.DTOs.SimularMultiplosVinculosInssRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class PdfNaoUsado : CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces.ICompartilhamentoPdf
    {
        public Task CompartilharAsync(CalculosTrabalhistasTributarios.Application.DTOs.DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CompartilharResumoAsync(CalculosTrabalhistasTributarios.Application.DTOs.DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
