using MediatR;
using OficinaMecanica.Seguranca.Application.DTOs;

namespace OficinaMecanica.Seguranca.Application.Commands.Login;

public record LoginCommand(string Cpf, string Senha) : IRequest<LoginResponseDto>;
