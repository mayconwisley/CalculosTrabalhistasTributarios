using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos.Rescisao;

/// <summary>Seguro-desemprego estimado na 1ª solicitação, com os meses deste contrato e a remuneração atual como média.</summary>
/// <param name="Meses">Meses deste contrato nos 36 anteriores à dispensa, ou nos 24 do doméstico.</param>
/// <param name="Domestico">Pela regra do doméstico: um salário mínimo, em até 3 parcelas.</param>
internal sealed record EstimativaSeguroRescisao(int Meses, int Parcelas, ParcelaSeguroDesemprego Parcela, bool Domestico = false);
