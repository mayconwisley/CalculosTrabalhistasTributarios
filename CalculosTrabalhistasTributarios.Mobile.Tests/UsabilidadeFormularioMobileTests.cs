using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CalculosTrabalhistasTributarios.Mobile.Tests;

public sealed class UsabilidadeFormularioMobileTests
{
    [Theory]
    [InlineData("29022024", TipoCampoMobile.Data, "29/02/2024")]
    [InlineData("31022024", TipoCampoMobile.Data, "31/02/2024")]
    [InlineData("102026", TipoCampoMobile.Competencia, "10/2026")]
    [InlineData("13/2026", TipoCampoMobile.Competencia, "13/2026")]
    [InlineData("", TipoCampoMobile.Data, "")]
    [InlineData("1:30", TipoCampoMobile.Horas, "1:30")]
    [InlineData("130", TipoCampoMobile.Horas, "130")]
    [InlineData("1,5", TipoCampoMobile.Horas, "1,5")]
    public void Digitacao_insere_separadores_sem_mudar_unidade_ou_corrigir_valores(string entrada, TipoCampoMobile tipo, string esperado) =>
        Assert.Equal(esperado, DigitacaoPeriodoMobile.Formatar(entrada, tipo));

    [Fact]
    public async Task Erro_de_leitura_abre_secao_e_edicao_limpa_o_erro()
    {
        var vm = new FormularioTeste();
        vm.Secoes[0].Expandida = false;
        vm.Campos.Single(c => c.Chave == "valor").Valor = "inválido";
        await vm.CalcularCommand.ExecuteAsync(null);
        var campo = Assert.IsType<CampoMobile>(vm.PrimeiroCampoComErro);
        Assert.Equal("valor", campo.Chave);
        Assert.True(campo.TemErro);
        Assert.True(vm.Secoes[0].Expandida);
        Assert.Null(vm.Demonstrativo);
        campo.Valor = "3.500,00";
        Assert.False(campo.TemErro);
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Equal(3500m, vm.Demonstrativo?.Resultado);
        Assert.Null(vm.PrimeiroCampoComErro);
    }

    [Fact]
    public async Task Erro_em_grade_preserva_campo_e_abre_a_pagina_da_linha()
    {
        var vm = new FormularioTeste();
        var grade = vm.Grades[0];
        for (var i = 0; i < 15; i++) grade.AdicionarLinha();
        grade.Linhas[14].Campo("valor").Valor = "x";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.Same(grade.Linhas[14].Campo("valor"), vm.PrimeiroCampoComErro);
        Assert.Contains(grade.Linhas[14], grade.Pagina.Exibidas);
        Assert.Contains("Linha 15", vm.Mensagem);
    }

    [Fact]
    public async Task Importar_formulario_limpa_erros_do_preenchimento_anterior()
    {
        var vm = new FormularioTeste();
        var inicial = vm.ExportarDados();
        vm.Campos.Single(c => c.Chave == "valor").Valor = "inválido";
        await vm.CalcularCommand.ExecuteAsync(null);
        Assert.NotNull(vm.PrimeiroCampoComErro);
        Assert.True(vm.TentarImportarDados(inicial).Sucesso);
        Assert.Null(vm.PrimeiroCampoComErro);
        Assert.All(vm.Campos, c => Assert.False(c.TemErro));
        Assert.Null(vm.Mensagem);
    }

    [Fact]
    public void Memoria_extensa_e_verbas_sao_paginadas_sem_perder_linhas()
    {
        var formulas = Enumerable.Range(1, 1000).Select(i => new FormulaDto($"Fórmula {i}", $"Operação {i}")).ToArray();
        var dto = new DemonstrativoDto("Teste", "10/2026", [], Enumerable.Range(1, 30).Select(i => new VerbaDto($"Verba {i}", "", i)).ToArray(), [], [], [new("Grupo", "Base", formulas)], ["Premissa"]);
        var detalhes = new DetalhesResultadoMobile(dto);
        Assert.Equal(12, detalhes.Proventos.Exibidas.Count);
        var linhas = new List<LinhaMemoriaMobile>();
        do
        {
            linhas.AddRange(detalhes.Memoria.Exibidas);
            if (!detalhes.Memoria.ProximaCommand.CanExecute(null)) break;
            detalhes.Memoria.ProximaCommand.Execute(null);
        } while (true);
        Assert.Equal(1001, linhas.Count);
        Assert.Equal("Operação 1000", linhas[^1].Texto);
        Assert.Equal(465m, dto.Resultado);
        Assert.Equal(1000, dto.Memoria[0].Formulas.Count);
    }

    internal sealed class FormularioTeste : CalculadoraTrabalhistaViewModel
    {
        public FormularioTeste() : base(new PdfTeste(), NullLogger.Instance)
        {
            Secao("dados", "Remuneração");
            Adicionar("valor", "Salário (R$)", TipoCampoMobile.Moeda, "0,00");
            AdicionarGrade("linhas", "Parcelas", "", "Linha", 30, [new("valor", "Valor da parcela", TipoCampoMobile.Moeda, "0,00")]);
        }
        public override string Titulo => "Teste";
        public override string Descricao => "Teste";
        protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
        {
            var valor = Numero("valor");
            Grade("linhas").Ler(l => LeituraCampoMobile.Numero(l.Campo("valor")));
            return Task.FromResult<Result<DemonstrativoDto>>(new DemonstrativoDto("Teste", "", [], [new("Salário", "", valor)], [], [], [], []));
        }
    }
    private sealed class PdfTeste : ICompartilhamentoPdf
    {
        public Task CompartilharAsync(DemonstrativoDto demonstrativo, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
