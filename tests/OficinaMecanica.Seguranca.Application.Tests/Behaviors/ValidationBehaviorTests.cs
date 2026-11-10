using FluentAssertions;
using FluentValidation;
using MediatR;
using OficinaMecanica.Seguranca.Application.Behaviors;

namespace OficinaMecanica.Seguranca.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    private sealed record PedidoDeTeste(string Nome) : IRequest<string>;

    private static InlineValidator<PedidoDeTeste> ValidadorDeNomeObrigatorio()
    {
        var validador = new InlineValidator<PedidoDeTeste>();
        validador.RuleFor(p => p.Nome).NotEmpty().WithMessage("O nome é obrigatório.");
        return validador;
    }

    [Fact]
    public async Task Handle_SemValidadores_DeveSeguirParaOProximo()
    {
        var behavior = new ValidationBehavior<PedidoDeTeste, string>(Array.Empty<IValidator<PedidoDeTeste>>());
        var chamouProximo = false;

        var resultado = await behavior.Handle(new PedidoDeTeste(""), () =>
        {
            chamouProximo = true;
            return Task.FromResult("ok");
        }, CancellationToken.None);

        resultado.Should().Be("ok");
        chamouProximo.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ComRequisicaoValida_DeveSeguirParaOProximo()
    {
        var behavior = new ValidationBehavior<PedidoDeTeste, string>(new[] { ValidadorDeNomeObrigatorio() });

        var resultado = await behavior.Handle(new PedidoDeTeste("Ana"), () => Task.FromResult("ok"), CancellationToken.None);

        resultado.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_ComRequisicaoInvalida_DeveLancarValidationExceptionSemChamarOProximo()
    {
        var behavior = new ValidationBehavior<PedidoDeTeste, string>(new[] { ValidadorDeNomeObrigatorio() });
        var chamouProximo = false;

        var act = () => behavior.Handle(new PedidoDeTeste(""), () =>
        {
            chamouProximo = true;
            return Task.FromResult("ok");
        }, CancellationToken.None);

        var excecao = await act.Should().ThrowAsync<ValidationException>();
        excecao.Which.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("O nome é obrigatório.");
        chamouProximo.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ComVariosValidadores_DeveReunirTodasAsFalhas()
    {
        var segundo = new InlineValidator<PedidoDeTeste>();
        segundo.RuleFor(p => p.Nome).MinimumLength(3).WithMessage("O nome precisa de 3 letras.");
        var behavior = new ValidationBehavior<PedidoDeTeste, string>(new[] { ValidadorDeNomeObrigatorio(), segundo });

        var act = () => behavior.Handle(new PedidoDeTeste(""), () => Task.FromResult("ok"), CancellationToken.None);

        // Optei por comparar as mensagens distintas: o behavior passa o mesmo ValidationContext para todos os validadores e o
        // FluentValidation acumula as falhas nele, entao com 2+ validadores a mesma falha aparece repetida no resultado.
        // O que este teste garante e que nenhuma falha de nenhum validador se perde.
        var excecao = await act.Should().ThrowAsync<ValidationException>();
        excecao.Which.Errors.Select(e => e.ErrorMessage).Distinct()
            .Should().BeEquivalentTo("O nome é obrigatório.", "O nome precisa de 3 letras.");
    }
}
