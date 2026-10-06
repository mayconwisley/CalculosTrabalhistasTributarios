using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

public sealed class SimularMediaVerbasVariaveisUseCase : ISimularDemonstrativoUseCase<SimularMediaVerbasVariaveisRequest>
{
    public Task<Result<DemonstrativoDto>> ExecutarAsync(SimularMediaVerbasVariaveisRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultado = CalculadoraMediaVerbasVariaveis.Calcular(request.Meses, request.Divisor);
        if (resultado.Falhou)
            return Task.FromResult<Result<DemonstrativoDto>>(resultado.Erro);
        var media = resultado.Valor;
        var primeiro = media.Meses[0].Competencia;
        var ultimo = media.Meses[^1].Competencia;
        var componentes = new (string Nome, decimal Total, decimal Media)[]
        {
            ("Comissões", media.TotalComissoes, media.MediaComissoes),
            ("DSR informado", media.TotalDsr, media.MediaDsr),
            ("Horas extras", media.TotalHorasExtras, media.MediaHorasExtras),
            ("Adicionais", media.TotalAdicionais, media.MediaAdicionais),
            ("Outras variáveis", media.TotalOutras, media.MediaOutras)
        };
        var formulas = componentes.Select(item => new FormulaDto(item.Nome,
            $"{Formato.Moeda(item.Total)} ÷ {media.Divisor} = {Formato.Moeda(item.Media)}")).ToArray();
        var memoria = new List<GrupoMemoriaDto>
        {
            new("Médias por verba", $"Total: {Formato.Moeda(media.MediaTotal)} ao mês", formulas)
        };
        memoria.AddRange(media.Meses.Select(mes => new GrupoMemoriaDto($"{mes.Competencia:MM/yyyy}",
            Formato.Moeda(mes.Comissoes + mes.Dsr + mes.HorasExtras + mes.Adicionais + mes.Outras),
            [
                new("Comissões", Formato.Moeda(mes.Comissoes)), new("DSR", Formato.Moeda(mes.Dsr)),
                new("Horas extras", Formato.Moeda(mes.HorasExtras)), new("Adicionais", Formato.Moeda(mes.Adicionais)),
                new("Outras", Formato.Moeda(mes.Outras))
            ])));

        var demonstrativo = new DemonstrativoDto(
            "Média de verbas variáveis",
            $"Período {primeiro:MM/yyyy} a {ultimo:MM/yyyy}",
            [
                new("Média mensal total", Formato.Moeda(media.MediaTotal), $"{media.Meses.Count} competência(s), divisor {media.Divisor}"),
                new("Comissões + DSR", Formato.Moeda(media.MediaComissoes + media.MediaDsr), "Médias separadas para conferência"),
                new("Horas extras", Formato.Moeda(media.MediaHorasExtras), "Média mensal informada"),
                new("Adicionais e outras", Formato.Moeda(media.MediaAdicionais + media.MediaOutras), "Médias mensais informadas")
            ],
            [new VerbaDto("Média mensal total", $"{Formato.Moeda(media.Total)} ÷ {media.Divisor}", media.MediaTotal)],
            [], [], memoria,
            [
                "O total da média é calculado a partir da soma de todas as verbas dividida pelo divisor. A soma das médias individuais pode diferir um centavo por arredondamento.",
                "Inclua apenas parcelas de natureza salarial. Comissões pagas já com DSR não devem ter o mesmo repouso lançado outra vez na coluna DSR.",
                "Férias por comissão consideram, em regra, os 12 meses anteriores à concessão; adicionais e horas de valor não uniforme seguem o período e a atualização previstos no art. 142 da CLT. Para 13º, confira os meses trabalhados no ano e os ajustes de dezembro.",
                "O divisor é informado pelo usuário para refletir o período efetivamente aplicável ao vínculo e à verba. Confira a convenção coletiva e os meses sem pagamento antes de levar esta média a férias, 13º ou rescisão."
            ],
            RotuloProventos: "Média apurada", RotuloResultado: "Média mensal total");
        return Task.FromResult<Result<DemonstrativoDto>>(demonstrativo);
    }
}
