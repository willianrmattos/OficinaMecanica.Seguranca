using OficinaMecanica.Seguranca.Domain.Entities;
using OficinaMecanica.Seguranca.Domain.Enums;
using OficinaMecanica.Seguranca.Domain.ValueObjects;

namespace OficinaMecanica.Seguranca.Tests.Common.Builders;

public class UsuarioBuilder
{
    private string _cpf = "52998224725";
    private string _senhaHash = "hash-fake";
    private PerfilUsuario _perfil = PerfilUsuario.Admin;

    public UsuarioBuilder ComCpf(string cpf) { _cpf = cpf; return this; }
    public UsuarioBuilder ComSenhaHash(string senhaHash) { _senhaHash = senhaHash; return this; }
    public UsuarioBuilder ComPerfil(PerfilUsuario perfil) { _perfil = perfil; return this; }

    public Usuario Build() => new(Cpf.Criar(_cpf), _senhaHash, _perfil);
}
