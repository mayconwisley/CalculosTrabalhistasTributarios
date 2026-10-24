using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RegraSimples = CalculosTrabalhistasTributarios.Domain.Tributacao.CalculadoraSimplesNacional;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class SimplesNacionalMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private const int LimiteMeses = 24;

    // Mesma ordem de opções do desktop; o primeiro item é o padrão.
    private static readonly AtividadeSimples[] Atividades =
    [
        AtividadeSimples.ServicosFatorR, AtividadeSimples.ServicosAnexoIII, AtividadeSimples.ServicosAnexoIV,
        AtividadeSimples.Comercio, AtividadeSimples.Industria
    ];

    private readonly ISimularDemonstrativoUseCase<SimularSimplesNacionalRequest> _simulador;
    private readonly GradeMobile _meses;

    public override string Titulo => "Simples Nacional e fator r";
    public override string Descricao => "Calcule o DAS pela receita e pela folha de 12 meses, o fator r e o anexo, e o efeito do pró-labore no custo do PJ.";

    public SimplesNacionalMobileViewModel(ISimularDemonstrativoUseCase<SimularSimplesNacionalRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<SimplesNacionalMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Secao("apuracao", "Apuração");
        Adicionar("periodo", "Período de apuração (mm/aaaa)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês do DAS. Define a tabela e os meses que formam a receita e a folha de 12 meses.");
        Adicionar("atividade", "Atividade", TipoCampoMobile.Opcao,
            dica: "O anexo depende da atividade do CNAE e do serviço prestado (Resolução CGSN 140/2018, art. 25). Confira o enquadramento da empresa.",
            opcoes: ["Serviços sujeitos ao fator r (Anexo III ou V)", "Serviços do Anexo III", "Serviços do Anexo IV", "Comércio (Anexo I)", "Indústria (Anexo II)"]);
        Adicionar("inicio", "Início das atividades (opcional) (mm/aaaa)", TipoCampoMobile.Competencia, "",
            "Mês de abertura, só se a empresa tem menos de 14 meses de atividade. Deixe vazio nos demais casos.");
        Adicionar("receitaMes", "Receita bruta do mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Receita do período de apuração, sobre a qual incide a alíquota efetiva.");
        Adicionar("folhaMes", "Folha do mês com encargos (R$)", TipoCampoMobile.Moeda, "0,00",
            "Usada só no 1º mês de atividade até 2026, para o fator r do próprio mês.");

        Secao("proLaboreSecao", "Pró-labore e CPP");
        Adicionar("proLabore", "Pró-labore mensal atual (R$)", TipoCampoMobile.Moeda, "0,00",
            "Pró-labore do sócio hoje, para estimar o INSS e o IRRF de um aumento que leve o fator r a 28%.");
        Adicionar("dependentes", "Dependentes do sócio", TipoCampoMobile.Inteiro, "0", "Dependentes do IRRF sobre o pró-labore.");
        Adicionar("remuneracoes", "Salários e pró-labore do mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Base da CPP de 20% recolhida fora do DAS no Anexo IV.");

        Secao("geradorSecao", "Gerar meses anteriores");
        Adicionar("receitaMensal", "Receita por mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Sugerida nas competências novas de Gerar meses; ajuste cada mês na grade.");
        Adicionar("folhaMensal", "Folha por mês (R$)", TipoCampoMobile.Moeda, "0,00",
            "Folha com encargos sugerida nas competências novas.");

        _meses = AdicionarGrade("meses", "Receita e folha dos meses anteriores",
            "Competências que formam a receita (RBT12) e a folha (FS12) de 12 meses do período de apuração. Gerar novamente preserva os valores dos meses que continuam.",
            "Mês", LimiteMeses,
            [
                new("competencia", "Competência (mm/aaaa)", TipoCampoMobile.Competencia),
                new("receita", "Receita bruta (R$)", TipoCampoMobile.Moeda, "0,00"),
                new("folha", "Folha com encargos (R$)", TipoCampoMobile.Moeda, "0,00")
            ]);
        _meses.GerarCommand = new RelayCommand(GerarMeses);
        _meses.RotuloGerar = "Gerar meses";
        AtualizarVisibilidade();
    }

    protected override void AoAlterar(CampoMobile campo)
    {
        if (campo.Chave == "atividade") AtualizarVisibilidade();
    }

    private void AtualizarVisibilidade()
    {
        var atividade = Atividades[Math.Clamp(Campo("atividade").Indice, 0, Atividades.Length - 1)];
        var fatorR = atividade == AtividadeSimples.ServicosFatorR;
        var anexoIV = atividade == AtividadeSimples.ServicosAnexoIV;
        Campo("proLabore").Visivel = Campo("dependentes").Visivel = fatorR;
        Campo("remuneracoes").Visivel = anexoIV;
        Campo("proLaboreSecao").Visivel = fatorR || anexoIV;
    }

    private DateOnly? InicioAtividade() => Texto("inicio").Length == 0 ? null : Competencia("inicio");

    private void GerarMeses()
    {
        try
        {
            var periodo = Competencia("periodo");
            var inicio = InicioAtividade();
            if (inicio > periodo)
                throw new EntradaInvalidaException("Início das atividades: não pode ser posterior ao período de apuração.");
            var receita = Numero("receitaMensal");
            var folha = Numero("folhaMensal");
            if (receita < 0m || folha < 0m)
                throw new EntradaInvalidaException("Informe receita e folha por mês positivas ou zero, por exemplo 20.000,00.");

            var janela = RegraSimples.Janela(periodo, inicio);
            var chaves = janela.Meses.Select(mes => mes.ToString("MM/yyyy", Cultura)).ToArray();
            var foraDaJanela = _meses.Linhas.Count(linha => !chaves.Contains(linha.Campo("competencia").Valor.Trim()));
            if (chaves.Length + foraDaJanela > LimiteMeses)
                throw new EntradaInvalidaException($"Remova as linhas fora do período antes de gerar: a grade aceita até {LimiteMeses} linhas.");

            var existentes = _meses.Linhas.ToHashSet();
            _meses.Gerar(chaves, "competencia");
            var novas = 0;
            foreach (var linha in _meses.Linhas.Where(linha => !existentes.Contains(linha)))
            {
                linha.Campo("receita").Valor = receita.ToString("N2", Cultura);
                linha.Campo("folha").Valor = folha.ToString("N2", Cultura);
                novas++;
            }
            var aviso = chaves.Length == 0
                ? "Neste mês de atividade não há meses anteriores a informar: o cálculo usa a receita e a folha do próprio mês ou a 1ª faixa."
                : $"{chaves.Length} competência(s), de {chaves[0]} a {chaves[^1]}: {chaves.Length - novas} preservada(s), {novas} nova(s)."
                  + (janela.Regra2027 ? " Desde 2027, o mês anterior ao período não entra." : "");
            if (foraDaJanela > 0)
                aviso += $" {foraDaJanela} linha(s) fora do período foram mantidas no fim da grade; remova-as se não fizerem parte do cálculo.";
            _meses.Aviso = aviso;
        }
        catch (EntradaInvalidaException erro) { _meses.Aviso = erro.Message; }
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var meses = _meses.Ler(linha => new MesSimples(LeituraCampoMobile.Competencia(linha.Campo("competencia")),
            LeituraCampoMobile.Numero(linha.Campo("receita")), LeituraCampoMobile.Numero(linha.Campo("folha"))));
        var atividade = Atividades[Opcao("atividade")];
        var fatorR = atividade == AtividadeSimples.ServicosFatorR;
        // Campos ocultos pela atividade não participam do cálculo, mesmo que tenham valores de antes da troca.
        var entrada = new EntradaSimples(Competencia("periodo"), atividade, Numero("receitaMes"), Numero("folhaMes"), InicioAtividade(), meses,
            atividade == AtividadeSimples.ServicosAnexoIV ? Numero("remuneracoes") : 0m);
        return _simulador.ExecutarAsync(new SimularSimplesNacionalRequest(entrada,
            fatorR ? Numero("proLabore") : 0m, fatorR ? Inteiro("dependentes") : 0), cancellationToken);
    }
}
