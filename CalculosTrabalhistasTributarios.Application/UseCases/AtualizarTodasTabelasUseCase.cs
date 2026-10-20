using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Consulta, uma de cada vez, as fontes de todas as tabelas, com as mesmas regras de confirmação da atualização individual.
/// Valor simplificado, dedução por dependente e redução mensal vêm da mesma página do IRRF, por isso não são consultados à parte.
/// Uma fonte fora do ar vira falha daquela tabela e preserva os dados locais; o cancelamento interrompe as que faltam.
/// </summary>
public sealed class AtualizarTodasTabelasUseCase(IAtualizadorTabelas atualizador) : IAtualizarTodasTabelasUseCase
{
    private static readonly (TipoTabelaTributaria Tipo, string Nome)[] Tabelas =
    [
        (TipoTabelaTributaria.Inss, "Tabela INSS"),
        (TipoTabelaTributaria.Irrf, "Tabela IRRF, simplificado, dependente e redução"),
        (TipoTabelaTributaria.SalarioMinimo, "Salário mínimo"),
        (TipoTabelaTributaria.SalarioFamilia, "Salário-família"),
        (TipoTabelaTributaria.SeguroDesemprego, "Seguro-desemprego"),
        (TipoTabelaTributaria.Plr, "Tabela PLR"),
        (TipoTabelaTributaria.DescontoMinimo, "Desconto mínimo"),
        (TipoTabelaTributaria.Inpc, "INPC"),
        (TipoTabelaTributaria.Ipca, "IPCA"),
        (TipoTabelaTributaria.IpcaE, "IPCA-E"),
        (TipoTabelaTributaria.Selic, "Selic"),
        (TipoTabelaTributaria.TaxaLegal, "Taxa legal"),
        (TipoTabelaTributaria.Tr, "TR")
    ];

    public int Total => Tabelas.Length;

    public async Task<IReadOnlyList<AtualizacaoDeTabela>> ExecutarAsync(IProgress<ProgressoAtualizacaoTabelas>? progresso, CancellationToken cancellationToken)
    {
        var resultados = new List<AtualizacaoDeTabela>(Tabelas.Length);
        foreach (var (tipo, nome) in Tabelas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progresso?.Report(new(resultados.Count, Tabelas.Length, nome));
            var resultado = await atualizador.AtualizarAsync(tipo, cancellationToken);
            // O atualizador devolve o tempo esgotado como falha; um cancelamento pedido não é falha da fonte.
            cancellationToken.ThrowIfCancellationRequested();
            resultados.Add(new(tipo, nome, resultado));
        }
        progresso?.Report(new(resultados.Count, Tabelas.Length, string.Empty));
        return resultados;
    }
}
