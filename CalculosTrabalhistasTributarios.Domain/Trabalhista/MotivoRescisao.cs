namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

/// <summary>
/// Motivo do desligamento. As rescisões antecipadas encerram o contrato a prazo antes do fim previsto, por iniciativa do
/// empregador (CLT, art. 479) ou do empregado (art. 480).
/// </summary>
public enum MotivoRescisao { DispensaSemJustaCausa, PedidoDeDemissao, Acordo, DispensaPorJustaCausa, TerminoDeContratoPorPrazo, RescisaoAntecipadaPeloEmpregador, RescisaoAntecipadaPeloEmpregado }
