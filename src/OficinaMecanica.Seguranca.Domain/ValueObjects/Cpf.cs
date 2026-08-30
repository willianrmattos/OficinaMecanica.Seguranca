using OficinaMecanica.Seguranca.Domain.Common;
using OficinaMecanica.Seguranca.Domain.Exceptions;

namespace OficinaMecanica.Seguranca.Domain.ValueObjects;

public class Cpf : ValueObject
{
    public string Numero { get; }

    private Cpf(string numero)
    {
        Numero = numero;
    }

    public static Cpf Criar(string numero)
    {
        var apenasDigitos = new string((numero ?? string.Empty).Where(char.IsDigit).ToArray());

        if (apenasDigitos.Length != 11 || !ValidarCpf(apenasDigitos))
            throw new DomainException("CPF inválido. Informe um CPF (11 dígitos) válido.");

        return new Cpf(apenasDigitos);
    }

    private static bool ValidarCpf(string cpf)
    {
        if (cpf.Distinct().Count() == 1) return false;

        var multiplicadores1 = new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        var multiplicadores2 = new[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        var tempCpf = cpf[..9];
        var soma = 0;

        for (int i = 0; i < 9; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicadores1[i];

        var resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;
        var digito = resto.ToString();
        tempCpf += digito;

        soma = 0;
        for (int i = 0; i < 10; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicadores2[i];

        resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;
        digito += resto.ToString();

        return cpf.EndsWith(digito);
    }

    public string Formatado => Convert.ToUInt64(Numero).ToString(@"000\.000\.000\-00");

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Numero;
    }
}
