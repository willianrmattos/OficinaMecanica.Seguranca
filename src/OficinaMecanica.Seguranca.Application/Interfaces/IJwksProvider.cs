using OficinaMecanica.Seguranca.Application.DTOs;

namespace OficinaMecanica.Seguranca.Application.Interfaces;

public interface IJwksProvider
{
    Task<JwksDto> ObterJwksAsync(CancellationToken cancellationToken = default);
}
