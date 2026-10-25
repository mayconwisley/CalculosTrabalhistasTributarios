using System.Text.Json;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Cópia de segurança do histórico em JSON. A restauração só acrescenta: o histórico atual é preservado e um cálculo que já
/// existe não é repetido. Os limites protegem o aparelho de um arquivo grande demais ou alterado.
/// </summary>
public sealed class CopiaSegurancaHistoricoUseCase(IHistoricoCalculos historico, IImportacaoHistoricoCalculos importacao) : ICopiaSegurancaHistoricoUseCase
{
    public const string Formato = "CalculosTrabalhistasTributarios.Historico";
    public const int VersaoAtual = 1;
    public const int MaximoCalculos = 5_000;
    public const int MaximoCaracteres = 60_000_000;
    // O mesmo limite que a abertura de um cálculo salvo aceita.
    public const int MaximoCaracteresFormulario = 2_000_000;
    private const int MaximoCaracteresNome = 100;
    private const int MaximoCaracteresTipo = 200;
    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    public async Task<Result<ArquivoCopiaSegurancaDto>> ExportarAsync(string origem, DateTime agora, CancellationToken cancellationToken)
    {
        var calculos = await historico.ListarAsync(cancellationToken);
        if (calculos.Count == 0)
            return Erro.Validacao("Não há cálculos salvos para copiar. Use Salvar numa calculadora antes de fazer a cópia de segurança.");
        // Do mais antigo para o mais recente: restaurados nessa ordem, os cálculos alterados no mesmo segundo mantêm a ordem da lista.
        var copia = new CopiaSegurancaHistoricoDto(Formato, VersaoAtual, origem, agora,
            [.. calculos.Reverse().Select(calculo => new CalculoCopiaSegurancaDto(calculo.Tipo, calculo.Calculadora, calculo.Nome, calculo.Dados, calculo.CriadoEm, calculo.AlteradoEm))]);
        return new ArquivoCopiaSegurancaDto($"historico-calculos-{agora:yyyyMMdd-HHmm}.json", JsonSerializer.Serialize(copia, Opcoes), calculos.Count);
    }

    public async Task<Result<ResumoRestauracaoHistoricoDto>> RestaurarAsync(string conteudo, string origem, CancellationToken cancellationToken)
    {
        var leitura = Ler(conteudo, origem);
        if (leitura.Falhou) return leitura.Erro;
        var calculos = leitura.Valor;
        var incluidos = await importacao.IncluirAsync(
            [.. calculos.Select(calculo => new CalculoSalvoDto(0, calculo.Tipo, calculo.Calculadora, calculo.Nome.Trim(), calculo.Dados, calculo.CriadoEm, calculo.AlteradoEm))],
            cancellationToken);
        return new ResumoRestauracaoHistoricoDto(incluidos, calculos.Count - incluidos);
    }

    private static Result<IReadOnlyList<CalculoCopiaSegurancaDto>> Ler(string conteudo, string origem)
    {
        const string NaoReconhecido = "O arquivo escolhido não é uma cópia de segurança do histórico deste aplicativo. Escolha o arquivo .json gerado por Fazer cópia de segurança.";
        if (string.IsNullOrWhiteSpace(conteudo)) return Erro.Validacao(NaoReconhecido);
        if (conteudo.Length > MaximoCaracteres)
            return Erro.Validacao($"O arquivo tem mais de {MaximoCaracteres / 1_000_000} milhões de caracteres, acima do limite da cópia de segurança.");
        CopiaSegurancaHistoricoDto? copia;
        try { copia = JsonSerializer.Deserialize<CopiaSegurancaHistoricoDto>(conteudo); }
        catch (JsonException) { return Erro.Validacao(NaoReconhecido); }
        if (copia is null || copia.Formato != Formato || copia.Calculos is null) return Erro.Validacao(NaoReconhecido);
        if (copia.Versao > VersaoAtual)
            return Erro.Validacao("A cópia foi gerada por uma versão mais nova do aplicativo. Atualize o aplicativo antes de restaurá-la.");
        if (copia.Versao < 1) return Erro.Validacao(NaoReconhecido);
        if (copia.Origem != origem)
            return Erro.Validacao($"A cópia foi gerada no aplicativo {copia.Origem}. Os formulários só reabrem no aplicativo de origem; restaure-a lá.");
        if (copia.Calculos.Count == 0) return Erro.Validacao("A cópia de segurança não contém cálculos.");
        if (copia.Calculos.Count > MaximoCalculos)
            return Erro.Validacao($"A cópia tem {copia.Calculos.Count} cálculos; o limite é {MaximoCalculos}.");
        for (var i = 0; i < copia.Calculos.Count; i++)
        {
            var calculo = copia.Calculos[i];
            var posicao = $"Cálculo {i + 1} da cópia";
            if (calculo is null || string.IsNullOrWhiteSpace(calculo.Tipo) || calculo.Tipo.Length > MaximoCaracteresTipo
                || string.IsNullOrWhiteSpace(calculo.Calculadora) || calculo.Calculadora.Length > MaximoCaracteresTipo)
                return Erro.Validacao($"{posicao}: a calculadora não foi identificada. O arquivo pode ter sido alterado.");
            if (string.IsNullOrWhiteSpace(calculo.Nome) || calculo.Nome.Trim().Length > MaximoCaracteresNome)
                return Erro.Validacao($"{posicao}: o nome precisa ter de 1 a {MaximoCaracteresNome} caracteres. O arquivo pode ter sido alterado.");
            if (string.IsNullOrWhiteSpace(calculo.Dados) || calculo.Dados.Length > MaximoCaracteresFormulario)
                return Erro.Validacao($"{posicao} ({calculo.Nome.Trim()}): o formulário está vazio ou excede o tamanho permitido.");
            if (calculo.CriadoEm.Year < 2000 || calculo.AlteradoEm.Year < 2000)
                return Erro.Validacao($"{posicao} ({calculo.Nome.Trim()}): as datas de criação e alteração são inválidas.");
        }
        return Result<IReadOnlyList<CalculoCopiaSegurancaDto>>.Ok(copia.Calculos);
    }
}
