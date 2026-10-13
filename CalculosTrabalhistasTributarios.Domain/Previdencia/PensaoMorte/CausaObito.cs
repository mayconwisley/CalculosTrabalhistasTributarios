namespace CalculosTrabalhistasTributarios.Domain.Previdencia.PensaoMorte;

/// <summary>
/// A causa do óbito: o acidente de qualquer natureza dispensa as 18 contribuições e os 2 anos de união na duração da
/// pensão do cônjuge (Lei 8.213/1991, art. 77, § 2º-A); o acidente do trabalho e a doença profissional também levam a
/// aposentadoria por incapacidade que serve de base a 100% da média (EC 103/2019, art. 26, § 3º, II).
/// </summary>
public enum CausaObito { Comum, Acidente, AcidenteTrabalho }
