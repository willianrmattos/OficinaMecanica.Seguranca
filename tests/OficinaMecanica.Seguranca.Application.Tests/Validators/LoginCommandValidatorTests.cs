using FluentAssertions;
using OficinaMecanica.Seguranca.Application.Commands.Login;

namespace OficinaMecanica.Seguranca.Application.Tests.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ComDadosValidos_DeveSerValido()
    {
        var command = new LoginCommand("52998224725", "senha-123");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_SemCpf_DeveSerInvalido()
    {
        var command = new LoginCommand("", "senha-123");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Cpf");
    }

    [Fact]
    public async Task Validate_SemSenha_DeveSerInvalido()
    {
        var command = new LoginCommand("52998224725", "");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Senha");
    }
}
