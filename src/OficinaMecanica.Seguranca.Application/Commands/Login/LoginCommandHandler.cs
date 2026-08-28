using MediatR;
using OficinaMecanica.Seguranca.Application.DTOs;
using OficinaMecanica.Seguranca.Application.Exceptions;
using OficinaMecanica.Seguranca.Application.Interfaces;
using OficinaMecanica.Seguranca.Domain.Interfaces;

namespace OficinaMecanica.Seguranca.Application.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponseDto>
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(
        IUsuarioRepository usuarioRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _usuarioRepository.ObterPorNomeUsuarioAsync(request.NomeUsuario, cancellationToken);

        // Mesma mensagem pros tres casos (usuario inexistente, inativo ou senha errada) de proposito,
        // pra nao revelar se o usuario existe ou nao (evita user enumeration).
        if (usuario == null || !usuario.Ativo || !_passwordHasher.Verificar(request.Senha, usuario.SenhaHash))
            throw new AutenticacaoException("Usuário ou senha inválidos.");

        var token = await _tokenService.GerarTokenAsync(usuario, cancellationToken);

        return new LoginResponseDto(token, "Bearer", _tokenService.ExpiracaoMinutos);
    }
}
