using OficinaMecanica.Seguranca.Domain.Common;
using OficinaMecanica.Seguranca.Domain.Enums;
using OficinaMecanica.Seguranca.Domain.Exceptions;
using OficinaMecanica.Seguranca.Domain.ValueObjects;

namespace OficinaMecanica.Seguranca.Domain.Entities;

public class Usuario : AggregateRoot
{
    public Cpf Cpf { get; private set; }
    public string SenhaHash { get; private set; }
    public PerfilUsuario Perfil { get; private set; }
    public bool Ativo { get; private set; }
    public DateTime DataCriacao { get; private set; }

    private Usuario() { } // EF Core

    public Usuario(Cpf cpf, string senhaHash, PerfilUsuario perfil)
    {
        if (string.IsNullOrWhiteSpace(senhaHash))
            throw new DomainException("O hash da senha é obrigatório.");

        Cpf = cpf ?? throw new DomainException("O CPF do usuário é obrigatório.");
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
