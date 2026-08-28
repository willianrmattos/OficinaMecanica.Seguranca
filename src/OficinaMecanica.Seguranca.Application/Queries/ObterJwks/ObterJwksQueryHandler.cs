using MediatR;
using OficinaMecanica.Seguranca.Application.DTOs;
using OficinaMecanica.Seguranca.Application.Interfaces;

namespace OficinaMecanica.Seguranca.Application.Queries.ObterJwks;

public class ObterJwksQueryHandler : IRequestHandler<ObterJwksQuery, JwksDto>
{
    private readonly IJwksProvider _jwksProvider;

    public ObterJwksQueryHandler(IJwksProvider jwksProvider)
    {
        _jwksProvider = jwksProvider;
    }

    public async Task<JwksDto> Handle(ObterJwksQuery request, CancellationToken cancellationToken)
    {
        return await _jwksProvider.ObterJwksAsync(cancellationToken);
    }
}
