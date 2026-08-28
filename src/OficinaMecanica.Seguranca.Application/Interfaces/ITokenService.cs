using OficinaMecanica.Seguranca.Domain.Entities;

namespace OficinaMecanica.Seguranca.Application.Interfaces;

public interface ITokenService
{
    int ExpiracaoMinutos { get; }
    Task<string> GerarTokenAsync(Usuario usuario, CancellationToken cancellationToken = default);
}
