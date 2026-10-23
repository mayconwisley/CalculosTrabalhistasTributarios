using CalculosTrabalhistasTributarios.Application.DTOs;
using CalculosTrabalhistasTributarios.Application.Interfaces;
using CalculosTrabalhistasTributarios.Domain.Comum;
using CalculosTrabalhistasTributarios.Domain.Tributacao;
using CalculosTrabalhistasTributarios.Mobile.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Presentation.Trabalhista;

public sealed class InssMultiplosVinculosMobileViewModel : CalculadoraTrabalhistaViewModel
{
    private const int LimiteVinculos = 50;
    private const int LimiteIdentificacao = 120;
    private static readonly TipoVinculoInss[] Categorias =
    [
        TipoVinculoInss.Empregado, TipoVinculoInss.Domestico, TipoVinculoInss.Avulso,
        TipoVinculoInss.ContribuinteIndividual, TipoVinculoInss.ContribuinteIndividualEbas
    ];

    private readonly ISimularDemonstrativoUseCase<SimularMultiplosVinculosInssRequest> _simulador;

    public override string Titulo => "INSS em múltiplos vínculos";
    public override string Descricao => "Desconto por vínculo, com teto compartilhado e faixas progressivas. Informe os vínculos na ordem de desconto.";

    public InssMultiplosVinculosMobileViewModel(ISimularDemonstrativoUseCase<SimularMultiplosVinculosInssRequest> simulador,
        ICompartilhamentoPdf compartilhamento, ILogger<InssMultiplosVinculosMobileViewModel> logger) : base(compartilhamento, logger)
    {
        _simulador = simulador;
        Adicionar("competencia", "Competência (MM/yyyy)", TipoCampoMobile.Competencia, DateTime.Today.ToString("MM/yyyy", Cultura),
            "Mês comum a todos os vínculos; define as faixas e o teto do INSS.");
        var vinculos = AdicionarGrade("vinculos", "Vínculos em ordem de desconto",
            "A ordem define qual vínculo usa cada faixa e a parte disponível do teto. Na mesma fonte pagadora, coloque empregado antes de "
            + "contribuinte individual. Use apenas vínculos do RGPS da mesma competência; de 2 a 50 vínculos.",
            "Vínculo", LimiteVinculos,
            [
                new("categoria", "Categoria", TipoCampoMobile.Opcao, Dica: "Individual: 11% ou 20% (EBAS) sobre a remuneração, dentro do teto restante.",
                    Opcoes: ["Empregado", "Doméstico", "Avulso", "Individual (11%)", "Individual EBAS (20%)"]),
                new("remuneracao", "Remuneração (R$)", TipoCampoMobile.Moeda, "0,00", "Maior que zero, em reais e centavos."),
                new("identificacao", "Fonte pagadora (opcional)", TipoCampoMobile.Texto, "", "Nome da empresa ou do tomador; vazio usa \"Vínculo N\".")
            ]);
        vinculos.AdicionarLinha();
        vinculos.AdicionarLinha();
    }

    protected override Task<Result<DemonstrativoDto>> ApurarAsync(CancellationToken cancellationToken)
    {
        var competencia = Competencia("competencia");
        var grade = Grade("vinculos");
        if (grade.Linhas.Count < 2)
            throw new EntradaInvalidaException("Informe pelo menos dois vínculos, na ordem de desconto.");
        var numero = 0;
        var vinculos = grade.Ler(linha =>
        {
            numero++;
            var tipo = Categorias[LeituraCampoMobile.Opcao(linha.Campo("categoria"))];
            var campoRemuneracao = linha.Campo("remuneracao");
            var remuneracao = LeituraCampoMobile.Numero(campoRemuneracao);
            if (remuneracao <= 0m || remuneracao > 1_000_000_000m || decimal.Round(remuneracao, 2) != remuneracao)
                throw new EntradaInvalidaException($"{campoRemuneracao.Rotulo}: informe um valor maior que zero, até R$ 1 bilhão, em reais e centavos.");
            var identificacao = linha.Campo("identificacao").Valor.Trim();
            if (identificacao.Length > LimiteIdentificacao)
                throw new EntradaInvalidaException($"Fonte pagadora: use no máximo {LimiteIdentificacao} caracteres.");
            return new VinculoInss(identificacao.Length > 0 ? identificacao : $"Vínculo {numero}", tipo, remuneracao);
        });
        return _simulador.ExecutarAsync(new SimularMultiplosVinculosInssRequest(competencia, vinculos), cancellationToken);
    }
}
