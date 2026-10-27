using FluentValidation;

namespace GestoraWebApi.Validators
{
    /// <summary>
    /// Le regole della password, uguali ovunque se ne sceglie una (registrazione, reset da Admin,
    /// recupero con il link email). Identity da solo chiede meno (6 caratteri, un numero): senza
    /// queste regole ogni nuovo punto sarebbe una scorciatoia verso una password più debole.
    /// </summary>
    public static class RegolePassword
    {
        public static IRuleBuilderOptions<T, string> PasswordSicura<T>(this IRuleBuilder<T, string> regola) =>
            regola
                .NotEmpty().WithMessage("La password è obbligatoria.")
                .MinimumLength(8).WithMessage("La password deve avere almeno 8 caratteri.")
                .Matches("[A-Z]").WithMessage("La password deve contenere almeno una lettera maiuscola.")
                .Matches("[0-9]").WithMessage("La password deve contenere almeno un numero.")
                .Matches("[^a-zA-Z0-9]").WithMessage("La password deve contenere almeno un carattere speciale.");
    }
}
