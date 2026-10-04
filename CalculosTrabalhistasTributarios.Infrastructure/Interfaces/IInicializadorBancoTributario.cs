namespace CalculosTrabalhistasTributarios.Infrastructure.Interfaces;

public interface IInicializadorBancoTributario
{
    Task InicializarAsync(CancellationToken cancellationToken);
}
