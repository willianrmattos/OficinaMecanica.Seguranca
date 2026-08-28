using MediatR;
using OficinaMecanica.Seguranca.Application.DTOs;

namespace OficinaMecanica.Seguranca.Application.Queries.ObterJwks;

public record ObterJwksQuery : IRequest<JwksDto>;
