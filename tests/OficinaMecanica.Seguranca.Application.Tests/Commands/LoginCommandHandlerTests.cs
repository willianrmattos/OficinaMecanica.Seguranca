using FluentAssertions;
using Moq;
using OficinaMecanica.Seguranca.Application.Commands.Login;
using OficinaMecanica.Seguranca.Application.Exceptions;
using OficinaMecanica.Seguranca.Application.Interfaces;
using OficinaMecanica.Seguranca.Domain.Entities;
using OficinaMecanica.Seguranca.Domain.Interfaces;
using OficinaMecanica.Seguranca.Tests.Common.Builders;

namespace OficinaMecanica.Seguranca.Application.Tests.Commands;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _usuarioRepositoryMock.Object,
            _passwordHasherMock.Object,
            _tokenServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ComCredenciaisValidas_DeveRetornarToken()
    {
        var usuario = new UsuarioBuilder().ComCpf("52998224725").ComSenhaHash("hash-valido").Build();
        var command = new LoginCommand("52998224725", "senha-correta");

        _usuarioRepositoryMock
            .Setup(r => r.ObterPorCpfAsync("52998224725", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _passwordHasherMock
            .Setup(h => h.Verificar("senha-correta", "hash-valido"))
            .Returns(true);
        _tokenServiceMock
            .Setup(t => t.GerarTokenAsync(usuario, It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-gerado");
        _tokenServiceMock
            .SetupGet(t => t.ExpiracaoMinutos)
            .Returns(60);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Token.Should().Be("token-gerado");
        result.Tipo.Should().Be("Bearer");
        result.ExpiracaoMinutos.Should().Be(60);
    }

    [Fact]
    public async Task Handle_ComUsuarioNaoEncontrado_DeveLancarExcecao()
    {
        var command = new LoginCommand("11111111111", "qualquer-senha");

        _usuarioRepositoryMock
            .Setup(r => r.ObterPorCpfAsync("11111111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<AutenticacaoException>().WithMessage("*inválidos*");
    }

    [Fact]
    public async Task Handle_ComUsuarioInativo_DeveLancarExcecao()
    {
        var usuario = new UsuarioBuilder().ComCpf("52998224725").ComSenhaHash("hash-valido").Build();
        usuario.Desativar();
        var command = new LoginCommand("52998224725", "senha-correta");

        _usuarioRepositoryMock
            .Setup(r => r.ObterPorCpfAsync("52998224725", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _passwordHasherMock
            .Setup(h => h.Verificar("senha-correta", "hash-valido"))
            .Returns(true);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<AutenticacaoException>().WithMessage("*inválidos*");
    }

    [Fact]
    public async Task Handle_ComSenhaIncorreta_DeveLancarExcecao()
    {
        var usuario = new UsuarioBuilder().ComCpf("52998224725").ComSenhaHash("hash-valido").Build();
        var command = new LoginCommand("52998224725", "senha-errada");

        _usuarioRepositoryMock
            .Setup(r => r.ObterPorCpfAsync("52998224725", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _passwordHasherMock
            .Setup(h => h.Verificar("senha-errada", "hash-valido"))
            .Returns(false);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<AutenticacaoException>().WithMessage("*inválidos*");
    }
}
