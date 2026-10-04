using CalculosTrabalhistasTributarios.Domain.Trabalhista;
using CalculosTrabalhistasTributarios.Domain.Trabalhista.Rescisao;

namespace CalculosTrabalhistasTributarios.Application.Demonstrativos.Rescisao;

/// <summary>Nomes, descrições e trechos de fórmula da rescisão, usados no resumo, na memória e nas observações.</summary>
internal static class TextosRescisao
{
    public static string Remuneracao(ContratoRescindido c) =>
        c.Medias > 0m ? $"({Formato.Moeda(c.Salario)} + {Formato.Moeda(c.Medias)} de médias)" : Formato.Moeda(c.Salario);

    public static string Dias(decimal dias) => dias == 1m ? "1 dia" : $"{Formato.Numero(dias)} dias";

    public static string Aviso(MotivoRescisao motivo, AvisoPrevioRescisao aviso) => aviso.Cumprimento switch
    {
        CumprimentoAvisoPrevio.Indenizado => Dias(aviso.DiasIndenizados),
        CumprimentoAvisoPrevio.NaoCumpridoPeloEmpregado => "Descontado",
        _ when motivo is MotivoRescisao.DispensaPorJustaCausa or MotivoRescisao.TerminoDeContratoPorPrazo or MotivoRescisao.RescisaoAntecipadaPeloEmpregador or MotivoRescisao.RescisaoAntecipadaPeloEmpregado => "Não se aplica",
        _ when motivo == MotivoRescisao.PedidoDeDemissao => "30 dias",
        _ => Formato.Dias(aviso.DiasProporcionais)
    };

    public static string NomeMotivo(MotivoRescisao motivo) => motivo switch
    {
        MotivoRescisao.DispensaSemJustaCausa => "Dispensa sem justa causa",
        MotivoRescisao.PedidoDeDemissao => "Pedido de demissão",
        MotivoRescisao.Acordo => "Acordo entre as partes",
        MotivoRescisao.DispensaPorJustaCausa => "Dispensa por justa causa",
        MotivoRescisao.RescisaoAntecipadaPeloEmpregador => "Rescisão antecipada do contrato a prazo pelo empregador",
        MotivoRescisao.RescisaoAntecipadaPeloEmpregado => "Rescisão antecipada do contrato a prazo pelo empregado",
        _ => "Término de contrato por prazo determinado"
    };

    public static string DescreverMotivo(MotivoRescisao motivo) => motivo switch
    {
        MotivoRescisao.DispensaSemJustaCausa => "Dispensa sem justa causa: o trabalhador recebe todas as verbas, a multa de 40% do FGTS e pode sacar o FGTS.",
        MotivoRescisao.PedidoDeDemissao => "Pedido de demissão: não há multa do FGTS nem saque; o 13º e as férias proporcionais são devidos (Súmulas 157 e 261 do TST).",
        MotivoRescisao.Acordo => "Acordo (CLT, art. 484-A): metade do aviso indenizado, multa de 20% do FGTS e saque de 80% do saldo, inclusive da multa; as demais verbas são integrais.",
        MotivoRescisao.DispensaPorJustaCausa => "Justa causa: são devidos apenas o saldo de salário e as férias vencidas com 1/3.",
        MotivoRescisao.RescisaoAntecipadaPeloEmpregador => "Rescisão antecipada pelo empregador (CLT, art. 479): o empregado recebe metade da remuneração dos dias que faltavam, as verbas proporcionais e a multa de 40% do FGTS, e pode sacar o FGTS. Se o contrato tiver cláusula de rescisão antecipada (art. 481), valem as regras da dispensa sem justa causa, com aviso prévio.",
        MotivoRescisao.RescisaoAntecipadaPeloEmpregado => "Rescisão antecipada pelo empregado (CLT, art. 480): o empregado recebe saldo, 13º e férias proporcionais, sem multa nem saque do FGTS, e deve indenizar os prejuízos que o empregador comprovar, até a metade da remuneração dos dias que faltavam.",
        _ => "Término do contrato por prazo determinado no prazo: sem aviso prévio e sem multa do FGTS, com saque do FGTS."
    };

    /// <summary>Como as verbas do mês aparecem nas descrições: só o saldo, ou o saldo e os outros proventos.</summary>
    public static string VerbasDoMes(ContratoRescindido c) => c.OutrosProventos > 0m ? "saldo de salário e outros proventos" : "saldo de salário";
}
