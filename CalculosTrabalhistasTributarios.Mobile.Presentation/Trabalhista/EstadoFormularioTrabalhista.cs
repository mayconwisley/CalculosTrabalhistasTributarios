using System.Collections.ObjectModel;
using System.Globalization;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Historico;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

/// <summary>Estado e compatibilidade do formulário, sem comandos, navegação ou execução de cálculos.</summary>
internal sealed class EstadoFormularioTrabalhista
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    internal Dictionary<string, CampoMobile> Campos { get; } = [];
    private Dictionary<string, CampoMobile> _campos => Campos;
    internal ObservableCollection<GradeMobile> Grades { get; } = [];
    internal ObservableCollection<LinhaDepositoFgtsMobile> DepositosFgts { get; } = [];
    internal ObservableCollection<LinhaBeneficioMobile> BeneficiosDetalhados { get; } = [];
    internal ObservableCollection<LinhaLancamentoBancoHorasMobile> LancamentosBancoHoras { get; } = [];
    public DadosFormularioMobile ExportarDados(QuitacaoBancoHoras? quitacao, bool confirmada) => new(1,
        _campos.Values.Where(campo => campo.EhEntrada).ToDictionary(campo => campo.Chave, Capturar),
        Grades.ToDictionary(grade => grade.Chave, grade => (IReadOnlyList<IReadOnlyDictionary<string, ValorCampoMobile>>)
            grade.Linhas.Select(linha => (IReadOnlyDictionary<string, ValorCampoMobile>)linha.Campos.ToDictionary(campo => campo.Chave, Capturar)).ToArray()),
        DepositosFgts.Select(linha => new DepositoFgtsSalvoMobile(linha.Competencia, linha.Valor)).ToArray(),
        BeneficiosDetalhados.Select(linha => new BeneficioSalvoMobile(linha.Descricao, linha.Custo, linha.Participacao)).ToArray(),
        LancamentosBancoHoras.Select(linha => new LancamentoBancoSalvoMobile(linha.Data, linha.Tipo, linha.Horas, linha.Adicional, linha.Descricao)).ToArray(),
        quitacao, confirmada);

    public Result TentarImportarDados(DadosFormularioMobile dados, SituacaoBancoHoras? destinoQuitacao)
    {
        if (dados.Versao != 1 || dados.Campos is null || dados.Grades is null)
            return Erro.Validacao("O formato do formulário salvo não é compatível com esta versão.");
        foreach (var (chave, valor) in dados.Campos)
        {
            if (!_campos.TryGetValue(chave, out var campo) || campo.EhSecao || !ValorValido(campo, valor))
                return Erro.Validacao($"Campo {chave}: o valor salvo não é compatível com este formulário.");
        }
        foreach (var (chave, linhas) in dados.Grades)
        {
            var grade = Grades.FirstOrDefault(item => item.Chave == chave);
            if (grade is null || linhas is null || linhas.Count > grade.Limite)
                return Erro.Validacao($"Grade {chave}: as linhas salvas excedem o limite ou não pertencem ao formulário.");
            var colunas = grade.Colunas.ToDictionary(coluna => coluna.Chave,
                coluna => new CampoMobile(coluna.Chave, coluna.Rotulo, coluna.Dica, coluna.Tipo, coluna.Valor, coluna.Opcoes));
            foreach (var linha in linhas)
                if (linha is null || linha.Any(par => !colunas.TryGetValue(par.Key, out var campo) || !ValorValido(campo, par.Value)))
                    return Erro.Validacao($"Grade {chave}: uma linha contém campo ou opção inválida.");
        }
        if ((dados.DepositosFgts?.Count ?? 0) > 720 || (dados.Beneficios?.Count ?? 0) > 30 || (dados.LancamentosBanco?.Count ?? 0) > 500
            || (dados.LancamentosBanco?.Any(linha => linha is null || linha.Tipo is < 0 or > 1 || !TextoValido(linha.Data, 20)
                || !TextoValido(linha.Horas, 20) || !TextoValido(linha.Adicional, 40) || !TextoValido(linha.Descricao, 120)) ?? false)
            || (dados.DepositosFgts?.Any(linha => linha is null || !TextoValido(linha.Competencia, 20) || !TextoValido(linha.Valor, 40)) ?? false)
            || (dados.Beneficios?.Any(linha => linha is null || !TextoValido(linha.Descricao, 120) || !TextoValido(linha.Custo, 40)
                || !TextoValido(linha.Participacao, 40)) ?? false))
            return Erro.Validacao("Linhas salvas: há valores ou quantidades fora dos limites do formulário.");

        if (dados.QuitacaoBancoHoras is { } quitacao)
        {
            var chaveData = destinoQuitacao == SituacaoBancoHoras.Fechamento ? "competencia" : "desligamento";
            if (destinoQuitacao is null
                || !dados.Campos.TryGetValue(chaveData, out var campoData)
                || !DateOnly.TryParseExact(destinoQuitacao == SituacaoBancoHoras.Fechamento ? "01/" + campoData.Valor : campoData.Valor,
                    "dd/MM/yyyy", Cultura, System.Globalization.DateTimeStyles.None, out var dataDestino))
                return Erro.Validacao("Quitação do banco de horas: confira a competência ou a data de desligamento salva.");
            var validacao = quitacao.Validar(destinoQuitacao == SituacaoBancoHoras.Fechamento ? SituacaoBancoHoras.Fechamento : SituacaoBancoHoras.Rescisao, dataDestino);
            if (validacao.Falhou) return validacao;
        }

        foreach (var (chave, valor) in dados.Campos) Restaurar(_campos[chave], valor);
        foreach (var grade in Grades)
        {
            while (grade.Linhas.Count > 0) grade.Linhas.RemoveAt(grade.Linhas.Count - 1);
            if (!dados.Grades.TryGetValue(grade.Chave, out var linhas)) continue;
            foreach (var valores in linhas)
            {
                var linha = grade.AdicionarLinha();
                if (linha is null) break;
                foreach (var (chave, valor) in valores) Restaurar(linha.Campo(chave), valor);
            }
        }
        DepositosFgts.Clear();
        foreach (var linha in dados.DepositosFgts ?? []) DepositosFgts.Add(new() { Competencia = linha.Competencia, Valor = linha.Valor });
        BeneficiosDetalhados.Clear();
        foreach (var linha in dados.Beneficios ?? []) BeneficiosDetalhados.Add(new() { Descricao = linha.Descricao, Custo = linha.Custo, Participacao = linha.Participacao });
        LancamentosBancoHoras.Clear();
        foreach (var linha in dados.LancamentosBanco ?? []) LancamentosBancoHoras.Add(new()
        { Data = linha.Data, Tipo = linha.Tipo, Horas = linha.Horas, Adicional = linha.Adicional, Descricao = linha.Descricao });
        return Result.Ok();
    }

    private static ValorCampoMobile Capturar(CampoMobile campo) => new(campo.Valor, campo.Indice, campo.Marcado);
    private static bool TextoValido(string? texto, int limite) => texto is not null && texto.Length <= limite;
    private static bool ValorValido(CampoMobile campo, ValorCampoMobile? valor) => valor is not null
        && TextoValido(valor.Valor, 500)
        && (campo.Tipo != TipoCampoMobile.Opcao || valor.Indice >= 0 && valor.Indice < campo.Opcoes.Count);
    private static void Restaurar(CampoMobile campo, ValorCampoMobile valor)
    {
        campo.Valor = valor.Valor;
        campo.Indice = valor.Indice;
        campo.Marcado = valor.Marcado;
    }
}
