namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="AnoBase">Ano trabalhado; o abono é pago dois anos depois (o de 2024, em 2026).</param>
/// <param name="Meses">Meses trabalhados no ano-base; a fração de 15 dias ou mais conta como mês.</param>
/// <param name="RemuneracaoMedia">Média mensal das remunerações no ano-base.</param>
/// <param name="Limite">Limite da remuneração média do calendário; zero usa o publicado para o ano do pagamento.</param>
public sealed record SimularAbonoSalarialRequest(int AnoBase, int Meses, decimal RemuneracaoMedia, bool CadastradoHa5Anos, decimal Limite);
