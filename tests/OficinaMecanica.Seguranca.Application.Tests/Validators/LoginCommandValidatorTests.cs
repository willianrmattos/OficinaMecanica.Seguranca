using FluentAssertions;
using OficinaMecanica.Seguranca.Application.Commands.Login;

namespace OficinaMecanica.Seguranca.Application.Tests.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ComDadosValidos_DeveSerValido()
    {
        var command = new LoginCommand("admin", "senha-123");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_SemNomeUsuario_DeveSerInvalido()
    {
        var command = new LoginCommand("", "senha-123");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NomeUsuario");
    }

    [Fact]
    public async Task Validate_SemSenha_DeveSerInvalido()
    {
        var command = new LoginCommand("admin", "");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Senha");
    }
}
