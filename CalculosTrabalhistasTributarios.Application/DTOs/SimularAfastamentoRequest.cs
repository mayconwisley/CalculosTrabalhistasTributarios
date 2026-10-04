using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Inicio">Primeiro dia do afastamento ou da licença.</param>
/// <param name="Dias">Dias de afastamento na doença e no acidente; as licenças têm a duração da lei.</param>
/// <param name="Media">Média dos salários de contribuição, para estimar o auxílio; zero usa a remuneração.</param>
/// <param name="EmpresaCidada">A empresa aderiu ao Programa Empresa Cidadã, que prorroga as licenças (Lei 11.770/2008).</param>
public sealed record SimularAfastamentoRequest(TipoAfastamento Tipo, DateOnly Inicio, int Dias, decimal Remuneracao, decimal Media, bool EmpresaCidada);
