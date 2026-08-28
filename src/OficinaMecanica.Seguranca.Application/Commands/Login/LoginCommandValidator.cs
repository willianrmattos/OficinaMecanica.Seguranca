using FluentValidation;

namespace OficinaMecanica.Seguranca.Application.Commands.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.NomeUsuario).NotEmpty().WithMessage("O nome de usuário é obrigatório.");
        RuleFor(x => x.Senha).NotEmpty().WithMessage("A senha é obrigatória.");
    }
}
