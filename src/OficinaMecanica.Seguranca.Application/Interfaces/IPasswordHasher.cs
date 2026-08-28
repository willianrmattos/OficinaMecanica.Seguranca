namespace OficinaMecanica.Seguranca.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string senha);
    bool Verificar(string senha, string hash);
}
