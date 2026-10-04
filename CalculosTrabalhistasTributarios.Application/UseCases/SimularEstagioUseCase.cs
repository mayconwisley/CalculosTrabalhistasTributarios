using CalculosTrabalhistasTributarios.Application.Demonstrativos;
using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Extensoes;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Domain.Trabalhista;

namespace CalculosTrabalhistasTributarios.Application.UseCases;

/// <summary>
/// Bolsa do estagiário, que não tem vínculo de emprego: sem INSS, FGTS, 13º nem férias, mas com IRRF sobre a bolsa
/// e o recesso remunerado de 30 dias por ano, proporcional ao tempo de estágio (Lei 11.788/2008, arts. 3º e 13).
/// </summary>
public sealed class SimularEstagioUseCase(ITributacaoConsulta tributacaoConsulta) : ISimularDemonstrativoUseCase<SimularEstagioRequest>
{
    public async Task<Result<DemonstrativoDto>> ExecutarAsync(SimularEstagioRequest r, CancellationToken cancellationToken)
    {
        if (r.Bolsa <= 0m)
            return Erro.Validacao("Informe o valor da bolsa.");
        if (r.AuxilioTransporte < 0m || r.DiasRecessoGozados < 0m || r.Dependentes < 0)
            return Erro.Validacao("Os valores, os dias de recesso e os dependentes não podem ser negativos.");
        if (r.Fim < r.Inicio)
            return Erro.Validacao("O fim do estágio não pode ser anterior ao início.");

        var consultaTabelas = await tributacaoConsulta.ObterTabelasAsync(r.Competencia, cancellationToken);
        if (consultaTabelas.Falhou)
            return consultaTabelas.Erro;
        var irrf = consultaTabelas.Valor.CalcularIrrf(r.Bolsa, 0m, r.Dependentes);

        var meses = RegrasTrabalhistas.AvosFerias(r.Inicio, r.Fim);
        var direito = RegrasEstagio.DiasDeRecesso(meses);
        var saldo = Math.Max(0m, direito - r.DiasRecessoGozados);
        var valorRecesso = CalculadoraTributacao.Arredondar(r.Bolsa / 30m * saldo);

        var proventos = new List<VerbaDto> { new("Bolsa de estágio", "", r.Bolsa) };
        if (r.AuxilioTransporte > 0m) proventos.Add(new("Auxílio-transporte", "", r.AuxilioTransporte));
        var descontos = new List<VerbaDto> { new(MemoriaTributaria.DescricaoIrrf("IRRF sobre a bolsa", irrf), MemoriaTributaria.ReferenciaIrrf(irrf), irrf.Imposto) };

        var observacoes = new List<string>
        {
            "O estágio não cria vínculo de emprego: não há INSS, FGTS, 13º nem férias. A bolsa tem IRRF pela tabela mensal, como rendimento do trabalho, e o seguro contra acidentes pessoais é obrigatório (Lei 11.788/2008, art. 9º, IV).",
            "O recesso é de 30 dias para cada ano de estágio, proporcional nos períodos menores, remunerado com a bolsa e de preferência nas férias escolares (art. 13). A lei não manda pagar o recesso não usufruído no fim do estágio; o termo de compromisso pode prever o pagamento.",
            "A jornada é de até 6 horas por dia e 30 por semana no ensino superior, médio e profissional, e de 4 horas por dia e 20 por semana na educação especial e nos anos finais do fundamental (art. 10)."
        };
        if (meses > RegrasEstagio.MesesMaximos)
            observacoes.Insert(0, "O estágio passou de 2 anos na mesma empresa, o máximo da lei, exceto para o estagiário com deficiência (art. 11). Sem essa exceção, o vínculo pode ser reconhecido como de emprego.");
        if (r.AuxilioTransporte > 0m)
            observacoes.Add("O auxílio-transporte ficou fora da base do IRRF, como ressarcimento de despesa; se a empresa o tratar como parte da bolsa, ele entra no rendimento.");

        return new DemonstrativoDto(
            "Estágio",
            $"Competência {Formato.Competencia(r.Competencia)} • estágio desde {Formato.Data(r.Inicio)}",
            [
                new("Líquido do estagiário", Formato.Moeda(r.Bolsa + r.AuxilioTransporte - irrf.Imposto), "Bolsa e auxílio menos o IRRF"),
                new("Recesso a usufruir", $"{Formato.Numero(saldo)} dias", $"{Formato.Numero(direito)} de direito em {meses} meses"),
                new("Valor do recesso", Formato.Moeda(valorRecesso), "Pago com a bolsa nos dias de descanso"),
                new("Tempo de estágio", meses == 1 ? "1 mês" : $"{meses} meses", meses > RegrasEstagio.MesesMaximos ? "Acima do limite de 2 anos" : "Limite: 24 meses")
            ],
            proventos,
            descontos,
            [new("Recesso proporcional a usufruir", $"{Formato.Numero(saldo)} dias", valorRecesso)],
            [
                new GrupoMemoriaDto("Recesso", $"{Formato.Numero(saldo)} dias", [
                    new("Tempo de estágio", $"De {Formato.Data(r.Inicio)} a {Formato.Data(r.Fim)}: {meses} meses (a fração de 15 dias conta como mês)"),
                    new("Recesso de direito", $"30 dias x {meses} ÷ 12 = {Formato.Numero(direito)} dias"),
                    new("Recesso a usufruir", $"{Formato.Numero(direito)} - {Formato.Numero(r.DiasRecessoGozados)} (já usufruídos) = {Formato.Numero(saldo)} dias"),
                    new("Valor", $"{Formato.Moeda(r.Bolsa)} ÷ 30 x {Formato.Numero(saldo)} dias = {Formato.Moeda(valorRecesso)}")]),
                MemoriaTributaria.Irrf("IRRF sobre a bolsa", irrf, "bolsa")
            ],
            observacoes);
    }
}
