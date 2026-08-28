namespace OficinaMecanica.Seguranca.Application.Exceptions;

// Excecao de Application, nao de Domain: "credenciais invalidas" nao e uma invariante
// da entidade Usuario, e uma falha de orquestracao que envolve repositorio + hasher.
public class AutenticacaoException : Exception
{
    public AutenticacaoException(string mensagem) : base(mensagem) { }
}
