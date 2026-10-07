using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Fgts;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

public sealed class CalculadoraConferenciaFgts(ISimularDemonstrativoUseCase<SimularConferenciaFgtsRequest> simulador) : CalculadoraBase
{
    private readonly CampoOpcaoViewModel _categoria = new("Categoria",
        [new("Empregado (8%)", CategoriaFgts.Empregado), new("Aprendiz (2%)", CategoriaFgts.Aprendiz), new("Doméstico (8% + 3,2%)", CategoriaFgts.Domestico)],
        "Define a alíquota: 8% em geral, 2% no contrato de aprendizagem; no doméstico, 8% de FGTS e 3,2% de indenização compensatória.") { Largura = 220 };
    private readonly CampoTextoViewModel _desligamento = new("Desligamento (opcional)", TipoCampo.Data, string.Empty,
        "Data de desligamento (dd/mm/aaaa), exigida para a competência rescisória e para levar os depósitos à rescisão. Deixe vazio sem rescisão.", permiteVazio: true);
    private readonly CampoConferenciaFgtsViewModel _competencias = new();

    public override string Titulo => "Conferência do FGTS";
    public override string Descricao => "Compare o FGTS devido em cada competência com o depositado e separe as diferenças mensais e rescisórias.";
    public override string InstrucaoInicial => "Escolha a categoria, gere as competências, informe a remuneração e o depósito de cada mês e selecione Calcular.";
    public override IReadOnlyList<CampoViewModel> Campos => [_categoria, _desligamento, _competencias];
    public override string NomeArquivoPdf => "conferencia-fgts.pdf";

    public override Dictionary<string, string> ExportarCampos()
    {
        var campos = base.ExportarCampos();
        campos[_competencias.Rotulo] = _competencias.Exportar();
        return campos;
    }

    public override void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        base.ImportarCampos(valores);
        if (valores.TryGetValue(_competencias.Rotulo, out var json))
            _competencias.Importar(json);
    }

    public override async Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken)
    {
        var lancamentos = _competencias.Ler();
        if (lancamentos.Falhou)
            return lancamentos.Erro;
        return await LerECalcularAsync(simulador, leitor => new SimularConferenciaFgtsRequest(
            _categoria.Valor<CategoriaFgts>(), lancamentos.Valor, leitor.DataOpcional(_desligamento)), cancellationToken);
    }
}
