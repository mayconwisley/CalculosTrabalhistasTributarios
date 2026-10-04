using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Reúne o que o usuário precisa atualizar: INSS, IRRF e salário mínimo mudam todo janeiro, e um cálculo com a tabela do
/// ano anterior sai errado sem nenhum erro aparente. Falhas na consulta da versão são ignoradas: o aviso é só uma conveniência.
/// </summary>
public sealed class VerificarAtualizacoesUseCase(IConsultaVersaoPublicada versoes, ITabelaTributariaService tabelas) : IVerificarAtualizacoesUseCase
{
    public async Task<IReadOnlyList<AvisoAtualizacaoDto>> ExecutarAsync(string versaoAtual, bool consultarVersao, DateOnly hoje, CancellationToken cancellationToken)
    {
        var avisos = new List<AvisoAtualizacaoDto>();

        var inss = await tabelas.ListarAsync(TipoTabelaTributaria.Inss, cancellationToken);
        if (inss.Count > 0 && inss.Max(registro => registro.Competencia) is var ultima && ultima.Year < hoje.Year)
            avisos.Add(new(TipoAvisoAtualizacao.TabelasDesatualizadas,
                $"As tabelas de {hoje.Year} ainda não estão cadastradas: a do INSS mais recente é de {ultima:MM/yyyy}. Atualize INSS, IRRF, salário mínimo, salário-família e seguro-desemprego.",
                null));

        if (consultarVersao && await versoes.ConsultarAsync(cancellationToken) is { Sucesso: true } consulta && EhMaisNova(consulta.Valor.Numero, versaoAtual))
            avisos.Add(new(TipoAvisoAtualizacao.NovaVersao,
                $"A versão {consulta.Valor.Numero} está disponível (você usa a {versaoAtual}). As tabelas que você editou são preservadas na atualização.",
                consulta.Valor.Endereco));

        return avisos;
    }

    public static bool EhMaisNova(string publicada, string atual) =>
        Version.TryParse(publicada, out var versaoPublicada) && Version.TryParse(atual, out var versaoAtual) && versaoPublicada > versaoAtual;
}
