using OficinaMecanica.Seguranca.Application.Interfaces;

namespace OficinaMecanica.Seguranca.Infrastructure.Services;

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string senha)
    {
        return BCrypt.Net.BCrypt.HashPassword(senha);
    }

    public bool Verificar(string senha, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(senha, hash);
        }
        catch (Exception)
        {
            // Hash malformado/incompativel (ex.: migrado de outro algoritmo) nao deve derrubar o login,
            // so ser tratado como senha incorreta.
            return false;
        }
    }
}
