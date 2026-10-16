namespace CalculosTrabalhistasTributarios.Presentation;

/// <summary>
/// Seção do manual (docs/MANUAL.md) de cada janela, pela chave da janela, que é a mesma do histórico, como
/// "Calculadora.Rescisao". Ao mudar a numeração do manual, atualize aqui: um teste confere que todas as seções existem.
/// </summary>
public static class SecoesDoManual
{
    private static readonly Dictionary<string, string> Secoes = new(StringComparer.Ordinal)
    {
        ["Calculadora.SimulacaoTributaria"] = "5",
        ["Pensao"] = "6.1",
        ["Calculadora.RevisaoPensao"] = "6.4",
        ["PensaoAtraso"] = "6.5",
        ["DebitoJudicial"] = "6.6",
        ["Estabilidade"] = "7",
        ["Calculadora.SalarioPeloLiquido"] = "8.1",
        ["Calculadora.HorasExtras"] = "8.2",
        ["Calculadora.DecimoTerceiro"] = "8.3",
        ["Calculadora.Ferias"] = "8.4",
        ["Calculadora.Rescisao"] = "8.5",
        ["Calculadora.CustoFuncionario"] = "8.6",
        ["Calculadora.ProLaboreAutonomo"] = "8.7",
        ["Calculadora.Adicionais"] = "8.8",
        ["Calculadora.SalarioFamilia"] = "8.9",
        ["Calculadora.Plr"] = "8.10",
        ["Calculadora.SeguroDesemprego"] = "8.12",
        ["Calculadora.CltPj"] = "8.13",
        ["Calculadora.Holerite"] = "8.14",
        ["Jornada"] = "8.15",
        ["Calculadora.IrpfAnual"] = "8.16",
        ["Calculadora.Dividendos"] = "8.17",
        ["Calculadora.TributoAtraso"] = "8.18",
        ["Calculadora.CorrecaoValor"] = "8.19",
        ["Calculadora.Domestico"] = "8.20",
        ["Calculadora.Estagio"] = "8.21",
        ["Calculadora.Intermitente"] = "8.22",
        ["Calculadora.CarneLeao"] = "8.23",
        ["Calculadora.TrabalhoExterior"] = "8.23.1",
        ["Calculadora.CreditoTrabalhador"] = "8.23.2",
        ["Calculadora.EmprestimoPessoal"] = "8.23.3",
        ["Calculadora.GanhoCapital"] = "8.24",
        ["Calculadora.Afastamento"] = "8.25",
        ["Calculadora.SaqueAniversario"] = "8.26",
        ["Calculadora.AbonoSalarial"] = "8.27",
        ["Calculadora.Comissoes"] = "8.28",
        ["Calculadora.InssMultiplosVinculos"] = "8.29",
        ["Calculadora.ReajusteRetroativo"] = "8.30",
        ["Calculadora.MediaVerbasVariaveis"] = "8.31",
        ["Calculadora.BancoHoras"] = "8.32",
        ["Calculadora.Rra"] = "8.33",
        ["Calculadora.SimplesNacional"] = "8.34",
        ["Calculadora.ConferenciaFgts"] = "8.35",
        ["Calculadora.Sobreaviso"] = "8.36",
        ["Calculadora.Transferencia"] = "8.37",
        ["Calculadora.Gorjetas"] = "8.38",
        ["Calculadora.ContribuicaoSegurado"] = "8.39",
        ["Calculadora.ResgatePrevidencia"] = "8.40",
        ["Calculadora.Aposentadoria"] = "8.41",
        ["Calculadora.PensaoMorte"] = "8.42",
        ["ComparacaoHistorico"] = "10.3"
    };

    /// <summary>Número da seção, como "8.15"; as tabelas ficam no capítulo 9; nulo para a tela inicial.</summary>
    public static string? Numero(string? chaveJanela) =>
        chaveJanela is null ? null
        : chaveJanela.StartsWith("Tabela.", StringComparison.Ordinal) ? "9"
        : Secoes.GetValueOrDefault(chaveJanela);

    public static IReadOnlyCollection<string> Chaves => Secoes.Keys;
}
