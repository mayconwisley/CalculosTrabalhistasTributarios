using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Presentation.Interfaces;
using System.Globalization;

namespace CalculosTrabalhistasTributarios.Presentation.ViewModels.Calculadoras;

/// <summary>Leitura dos campos no padrão brasileiro, com mensagens que dizem qual campo corrigir e como.</summary>
public abstract class CalculadoraBase : ICalculadora
{
    protected static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public abstract string Titulo { get; }
    public abstract string Descricao { get; }
    public abstract string InstrucaoInicial { get; }
    public abstract IReadOnlyList<CampoViewModel> Campos { get; }
    public abstract string NomeArquivoPdf { get; }

    // Campos trocados com as outras calculadoras; cada calculadora indica os seus, e os demais ficam de fora.
    protected virtual CampoTextoViewModel? CampoCompetencia => null;
    protected virtual CampoTextoViewModel? CampoSalario => null;
    protected virtual CampoTextoViewModel? CampoDependentes => null;

    public ContextoCalculo Contexto => new(
        CampoCompetencia is { } competencia && DateTime.TryParseExact(competencia.Valor.Trim(), "MM/yyyy", Cultura, DateTimeStyles.None, out var data) ? DateOnly.FromDateTime(data) : null,
        CampoSalario is { } salario && LeituraNumerica.TentarLer(salario.Valor, out var valor) && valor > 0m ? valor : null,
        CampoDependentes is { } dependentes && int.TryParse(dependentes.Valor.Trim(), NumberStyles.Integer, Cultura, out var quantidade) && quantidade >= 0 ? quantidade : null);

    public void Preencher(ContextoCalculo contexto)
    {
        if (CampoCompetencia is { } campoCompetencia && contexto.Competencia is { } competencia) campoCompetencia.Valor = competencia.ToString("MM/yyyy", Cultura);
        if (CampoSalario is { } campoSalario && contexto.Salario is { } salario && salario > 0m) campoSalario.Valor = salario.ToString("N2", Cultura);
        if (CampoDependentes is { } campoDependentes && contexto.Dependentes is { } dependentes) campoDependentes.Valor = dependentes.ToString(Cultura);
    }

    public abstract Task<Result<DemonstrativoDto>> CalcularAsync(CancellationToken cancellationToken);

    public Dictionary<string, string> ExportarCampos() => Campos.ToDictionary(campo => campo.Rotulo, campo => campo switch
    {
        CampoTextoViewModel texto => texto.Valor,
        CampoOpcaoViewModel opcao => opcao.Selecionada.Texto,
        _ => string.Empty
    });

    /// <summary>Rótulo antigo de cada campo renomeado, com o rótulo atual: o histórico guarda os valores pelo rótulo.</summary>
    protected virtual IReadOnlyDictionary<string, string> RotulosAnteriores { get; } = new Dictionary<string, string>();

    public void ImportarCampos(IReadOnlyDictionary<string, string> valores)
    {
        if (RotulosAnteriores.Count > 0)
        {
            var atualizados = new Dictionary<string, string>(valores);
            foreach (var (antigo, atual) in RotulosAnteriores)
                if (atualizados.Remove(antigo, out var valor))
                    atualizados.TryAdd(atual, valor);
            valores = atualizados;
        }

        // As opções primeiro: ao mudar, elas mostram campos e podem sugerir valores, que os textos salvos substituem.
        foreach (var opcao in Campos.OfType<CampoOpcaoViewModel>())
            if (valores.TryGetValue(opcao.Rotulo, out var texto) && opcao.Opcoes.FirstOrDefault(item => item.Texto == texto) is { } escolhida)
                opcao.Selecionada = escolhida;
        foreach (var campo in Campos.OfType<CampoTextoViewModel>())
            if (valores.TryGetValue(campo.Rotulo, out var valor))
                campo.Valor = valor;
    }

    protected static CampoTextoViewModel Competencia(string rotulo = "Competência", string? dica = null) =>
        new(rotulo, TipoCampo.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura), dica ?? "Mês e ano das tabelas de INSS e IRRF (MM/AAAA).");

    protected static CampoTextoViewModel Moeda(string rotulo, string? dica = null) => new(rotulo, TipoCampo.Moeda, "0,00", dica);

    protected static CampoTextoViewModel Inteiro(string rotulo, int inicial, string? dica = null) => new(rotulo, TipoCampo.Inteiro, inicial.ToString(Cultura), dica);

    /// <summary>
    /// Lê o formulário com <paramref name="montar"/> e, se todos os campos estão no formato certo, executa o cálculo;
    /// senão, devolve o erro do primeiro campo a corrigir.
    /// </summary>
    protected static async Task<Result<DemonstrativoDto>> LerECalcularAsync<TRequest>(ISimularDemonstrativoUseCase<TRequest> simulador, Func<LeitorFormulario, TRequest> montar, CancellationToken cancellationToken)
    {
        var leitor = new LeitorFormulario();
        var request = montar(leitor);
        return leitor.Erro is { } erro ? erro : await simulador.ExecutarAsync(request, cancellationToken);
    }
}
