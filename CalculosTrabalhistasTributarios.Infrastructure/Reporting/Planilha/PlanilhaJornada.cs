using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using static CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha.ComponentesPlanilha;

namespace CalculosTrabalhistasTributarios.Infrastructure.Reporting.Planilha;

/// <summary>Planilha da apuração do cartão de ponto, dia a dia e com os totais.</summary>
internal static class PlanilhaJornada
{
    internal static Task GerarJornadaAsync(SimulacaoJornadaDto a, string caminhoArquivo, CancellationToken cancellationToken) =>
        SalvarAsync(caminhoArquivo, cancellationToken, pasta =>
        {
            var aba = new Aba(pasta.AddWorksheet("Jornada"));
            aba.Titulo("Jornada pelas marcações de ponto", $"Competência {a.Competencia:MM/yyyy} • ponto de {a.Dias[0].Data:dd/MM/yyyy} a {a.Dias[^1].Data:dd/MM/yyyy}");
            var t = a.Totais;
            aba.Secao("Totais do período");
            aba.Par("Horas trabalhadas", Horas(t.Trabalhadas));
            aba.Par("Horas previstas", Horas(t.Previstas));
            aba.Par("Extras dos dias úteis (faixa 1)", Horas(t.ExtrasFaixa1));
            aba.Par("Extras noturnas dos dias úteis", Horas(t.ExtrasNoturnas));
            aba.Par("Extras em descansos e feriados (faixa 2)", Horas(t.ExtrasFaixa2));
            aba.Par("Horas noturnas (relógio)", Horas(t.Noturnas));
            aba.Par("Faltas (dias)", t.Faltas);
            aba.Par("Descansos perdidos", t.DescansosPerdidos);
            aba.Par("Atrasos e saídas antecipadas", Horas(t.Faltantes));
            aba.Par("Intervalo intrajornada suprimido", Horas(t.IntervaloSuprimido));
            aba.Par("Intervalo entre jornadas suprimido", Horas(t.InterjornadaSuprimida));
            aba.Par("Feriados em dias de semana", t.Feriados);

            aba.Secao("Dias");
            aba.Cabecalho("Data", "Tipo", "Prevista", "Trabalhadas", "Extras", "Noturnas", "Extras noturnas", "Atrasos", "Falta", "Intrajornada suprimida", "Entre jornadas suprimida");
            foreach (var dia in a.Dias)
                aba.Linha(dia.Data, dia.Tipo switch { TipoDia.Descanso => "Descanso", TipoDia.Feriado => "Feriado", _ => "Útil" }, Horas(dia.MinutosPrevistos), Horas(dia.MinutosTrabalhados),
                    Horas(dia.MinutosExtras), Horas(dia.MinutosNoturnos), Horas(dia.MinutosNoturnosExtras), Horas(dia.MinutosFaltantes), dia.Falta ? "Sim" : "",
                    Horas(dia.IntervaloSuprimido), Horas(dia.InterjornadaSuprimida));

            aba.Secao("Critérios");
            foreach (var criterio in a.Criterios)
                aba.Texto(criterio);
            Observacoes(aba, a.Observacoes);
            aba.Ajustar();
        });

    /// <summary>Duração em horas como fração do dia, o formato de horas do Excel, que soma acima de 24 horas com [h]:mm.</summary>
    private static Celula Horas(int minutos) => new(minutos / 1440m, "[h]:mm");
}
