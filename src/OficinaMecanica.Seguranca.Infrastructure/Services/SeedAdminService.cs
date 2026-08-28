using OficinaMecanica.Seguranca.Application.Interfaces;
using OficinaMecanica.Seguranca.Domain.Entities;
using OficinaMecanica.Seguranca.Domain.Enums;
using OficinaMecanica.Seguranca.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace OficinaMecanica.Seguranca.Infrastructure.Services;

// Sem interface de proposito - so um servico concreto chamado explicitamente no startup da Function.
public class SeedAdminService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;

    public SeedAdminService(
        IUsuarioRepository usuarioRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IConfiguration configuration)
    {
        _usuarioRepository = usuarioRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _usuarioRepository.ExisteAlgumAsync(cancellationToken))
            return;

        var nomeUsuario = _configuration["SeedAdmin:NomeUsuario"];
        var senhaInicial = _configuration["SeedAdmin:SenhaInicial"];

        if (string.IsNullOrWhiteSpace(nomeUsuario) || string.IsNullOrWhiteSpace(senhaInicial))
            throw new InvalidOperationException(
                "As configuracoes 'SeedAdmin:NomeUsuario' e 'SeedAdmin:SenhaInicial' sao obrigatorias para criar o usuario administrador inicial.");

        var usuario = new Usuario(nomeUsuario, _passwordHasher.Hash(senhaInicial), PerfilUsuario.Admin);

        await _usuarioRepository.AdicionarAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
