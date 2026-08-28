using OficinaMecanica.Seguranca.Domain.Common;
using OficinaMecanica.Seguranca.Domain.Enums;
using OficinaMecanica.Seguranca.Domain.Exceptions;

namespace OficinaMecanica.Seguranca.Domain.Entities;

public class Usuario : AggregateRoot
{
    public string NomeUsuario { get; private set; }
    public string SenhaHash { get; private set; }
    public PerfilUsuario Perfil { get; private set; }
    public bool Ativo { get; private set; }
    public DateTime DataCriacao { get; private set; }

    private Usuario() { } // EF Core

    public Usuario(string nomeUsuario, string senhaHash, PerfilUsuario perfil)
    {
        if (string.IsNullOrWhiteSpace(nomeUsuario))
            throw new DomainException("O nome de usuário é obrigatório.");

        if (string.IsNullOrWhiteSpace(senhaHash))
            throw new DomainException("O hash da senha é obrigatório.");

        NomeUsuario = nomeUsuario;
        SenhaHash = senhaHash;
        Perfil = perfil;
        Ativo = true;
        DataCriacao = DateTime.UtcNow;
    }

    public void AlterarSenha(string novaSenhaHash)
    {
        if (string.IsNullOrWhiteSpace(novaSenhaHash))
            throw new DomainException("O hash da senha é obrigatório.");

        SenhaHash = novaSenhaHash;
    }

    public void Desativar()
    {
        if (!Ativo)
            throw new DomainException("O usuário já está desativado.");

        Ativo = false;
    }
}
