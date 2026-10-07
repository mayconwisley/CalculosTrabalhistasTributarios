using CalculosTrabalhistasTributarios.Domain.Comum;

namespace CalculosTrabalhistasTributarios.Domain.Trabalhista;

public static class AnalisadorAntecipacaoFgts
{
    public static Result<AnaliseAntecipacaoFgts> Analisar(decimal baseFgts, decimal garantia, int mesAniversario, EntradaAnaliseAntecipacaoFgts entrada)
    {
        if (entrada.DataConsulta < new DateOnly(2025, 11, 1) || entrada.DataConsulta.Year > 2100)
            return Erro.Validacao("Data da análise: informe uma data entre 01/11/2025 e 31/12/2100, período das regras de nova antecipação simuladas.");
        if (mesAniversario is < 1 or > 12 || baseFgts <= 0m || baseFgts > 1_000_000_000_000m ||
            decimal.Round(baseFgts, 2) != baseFgts || decimal.Round(garantia, 2) != garantia || garantia < 0m || garantia > baseFgts ||
            !Enum.IsDefined(entrada.AdesaoHa90Dias) || !Enum.IsDefined(entrada.ContratoDesdeNovembro2025) || !Enum.IsDefined(entrada.ProximoSaqueComprometido))
            return Erro.Validacao("Confira saldo, garantia, mês de aniversário e as confirmações da análise de antecipação.");

        var aniversario = new DateOnly(entrada.DataConsulta.Year, mesAniversario, 1);
        if (entrada.DataConsulta.Month >= mesAniversario)
            aniversario = aniversario.AddYears(1);
        var motivos = new List<string>();
        var impedida = false;
        var pendente = false;
        var livre = baseFgts - garantia;
        if (entrada.DataConsulta.Month == mesAniversario)
        {
            pendente = true;
            motivos.Add("A análise ocorre no mês do aniversário. Confirme o processamento do repasse deste mês; o aniversário do ano seguinte é apenas a referência futura e não comprova quitação do contrato atual.");
        }
        // Resolução CCFGTS 1.130/2025 e orientação do agente operador:
        // https://www.fgts.gov.br/Paginas/trabalhador/saque/saque-aniversario.aspx
        if (livre == 0m)
        {
            impedida = true;
            motivos.Add("A base informada está integralmente bloqueada como garantia; não há saldo fora dessa garantia nesta análise.");
        }
        if (entrada.AdesaoHa90Dias == ConfirmacaoAntecipacaoFgts.Nao)
        {
            impedida = true;
            motivos.Add("A carência de 90 dias após a adesão ao saque-aniversário ainda não foi cumprida.");
        }
        else if (entrada.AdesaoHa90Dias == ConfirmacaoAntecipacaoFgts.NaoInformado)
        {
            pendente = true;
            motivos.Add("Confirme se decorreram 90 dias da adesão ao saque-aniversário e se o banco está autorizado a consultar o FGTS.");
        }
        if (entrada.ProximoSaqueComprometido == ConfirmacaoAntecipacaoFgts.Sim &&
            entrada.ContratoDesdeNovembro2025 == ConfirmacaoAntecipacaoFgts.Sim)
        {
            impedida = true;
            motivos.Add($"Foi informado contrato desde 01/11/2025 e o próximo saque ({aniversario:MM/yyyy}) está cedido: nova contratação depende da quitação da antecipação vigente do próximo saque. Novos depósitos não permitem uma segunda contratação para o mesmo saque anual.");
        }
        else if (entrada.ProximoSaqueComprometido != ConfirmacaoAntecipacaoFgts.Nao)
        {
            pendente = true;
            motivos.Add($"Confirme com o banco se o saque de {aniversario:MM/yyyy} está cedido e quais competências estão contratadas. O saldo bloqueado agregado não identifica parcelas ou anos. Contratos anteriores a 01/11/2025 exigem conferência do tratamento de transição.");
        }
        motivos.Add("Saldo fora da garantia não é limite de empréstimo. A contratação depende das competências livres, da consulta do agente operador e da análise do banco; o crédito líquido depende de juros e encargos do contrato.");
        var limite = entrada.DataConsulta < new DateOnly(2026, 11, 1) ? 5 : 3;
        motivos.Add($"Na data informada, novas antecipações abrangem no máximo {limite} saques anuais, de R$ 100 a R$ 500 por saque, com uma contratação por competência. Esses limites não asseguram aprovação nem representam valor líquido liberado.");
        return new AnaliseAntecipacaoFgts(impedida ? "Impedimento informado" : pendente ? "Não confirmada" : "Sujeita à análise do banco",
            aniversario, livre, limite, motivos);
    }
}
