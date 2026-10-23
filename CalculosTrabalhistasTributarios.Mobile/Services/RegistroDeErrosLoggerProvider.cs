using CalculosTrabalhistasTributarios.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CalculosTrabalhistasTributarios.Mobile.Services;

/// <summary>
/// Leva ao registro de erros as falhas que as telas tratam com LogError, como as de cálculo, PDF e histórico.
/// Só entram erros com exceção: a mensagem traz a calculadora, nunca os valores do formulário.
/// </summary>
public sealed class RegistroDeErrosLoggerProvider(IRegistroDeErros registro) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Registrador(registro, categoryName);

    public void Dispose() { }

    private sealed class Registrador(IRegistroDeErros registro, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel) && exception is not null)
                registro.Registrar(exception, $"{formatter(state, exception)} ({categoria})");
        }
    }
}
