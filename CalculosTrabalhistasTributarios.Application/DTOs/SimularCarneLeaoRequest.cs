namespace CalculosTrabalhistasTributarios.Application.DTOs;

/// <param name="Rendimentos">Honorários e outros rendimentos de trabalho recebidos de pessoas físicas ou do exterior.</param>
/// <param name="Alugueis">Aluguéis recebidos de pessoas físicas.</param>
/// <param name="DespesasAluguel">IPTU, condomínio e taxa de administração pagos pelo locador, abatidos do aluguel na base do imposto.</param>
/// <param name="LivroCaixa">Despesas do livro-caixa do profissional autônomo, até o valor dos rendimentos de trabalho.</param>
/// <param name="InssPago">Contribuição ao INSS paga no mês como contribuinte individual.</param>
/// <param name="PensaoPaga">Pensão alimentícia judicial ou por escritura pública paga no mês.</param>
public sealed record SimularCarneLeaoRequest(DateOnly Competencia, decimal Rendimentos, decimal Alugueis, decimal DespesasAluguel, decimal LivroCaixa, decimal InssPago, int Dependentes, decimal PensaoPaga);
