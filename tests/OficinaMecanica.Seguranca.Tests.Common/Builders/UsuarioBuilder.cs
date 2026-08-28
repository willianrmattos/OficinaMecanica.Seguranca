using Bogus;
using OficinaMecanica.Seguranca.Domain.Entities;
using OficinaMecanica.Seguranca.Domain.Enums;

namespace OficinaMecanica.Seguranca.Tests.Common.Builders;

public class UsuarioBuilder
{
    private static readonly Faker Faker = new("pt_BR");

    private string _nomeUsuario = Faker.Internet.UserName();
    private string _senhaHash = "hash-fake";
    private PerfilUsuario _perfil = PerfilUsuario.Admin;

    public UsuarioBuilder ComNomeUsuario(string nomeUsuario) { _nomeUsuario = nomeUsuario; return this; }
    public UsuarioBuilder ComSenhaHash(string senhaHash) { _senhaHash = senhaHash; return this; }
    public UsuarioBuilder ComPerfil(PerfilUsuario perfil) { _perfil = perfil; return this; }

    public Usuario Build() => new(_nomeUsuario, _senhaHash, _perfil);
}
