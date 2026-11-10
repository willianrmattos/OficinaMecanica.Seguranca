using FluentAssertions;
using Moq;
using OficinaMecanica.Seguranca.Application.DTOs;
using OficinaMecanica.Seguranca.Application.Interfaces;
using OficinaMecanica.Seguranca.Application.Queries.ObterJwks;

namespace OficinaMecanica.Seguranca.Application.Tests.Queries;

public class ObterJwksQueryHandlerTests
{
    [Fact]
    public async Task Handle_DeveDevolverOConjuntoDeChavesDoProvedor()
    {
        var esperado = new JwksDto(new[] { new JwkDto("RSA", "sig", "RS256", "kid-1", "modulo", "expoente") });
        var provedor = new Mock<IJwksProvider>();
        provedor.Setup(p => p.ObterJwksAsync(It.IsAny<CancellationToken>())).ReturnsAsync(esperado);
        var handler = new ObterJwksQueryHandler(provedor.Object);

        var resultado = await handler.Handle(new ObterJwksQuery(), CancellationToken.None);

        resultado.Should().BeSameAs(esperado);
        provedor.Verify(p => p.ObterJwksAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
