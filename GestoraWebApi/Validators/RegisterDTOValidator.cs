using FluentValidation;
using GestoraWebApi.Services.Auth.DTOs;

namespace GestoraWebApi.Validators
{
    public class RegisterDTOValidator : AbstractValidator<RegisterDTO>
    {
        public RegisterDTOValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Il nome utente è obbligatorio.")
                .MinimumLength(3).WithMessage("Il nome utente deve avere almeno 3 caratteri.")
                .MaximumLength(50).WithMessage("Il nome utente non può superare 50 caratteri.")
                .Must(RegoleUsername.SpaziValidi).WithMessage(RegoleUsername.MessaggioSpazi);

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("L'email è obbligatoria.")
                .EmailAddress().WithMessage("Inserire un indirizzo email valido.");

            RuleFor(x => x.Password).PasswordSicura();
        }
    }
}

namespace GestoraWebApi.Validators
{
    /// <summary>
    /// Il nome utente ammette spazi ("Fabio Iacurto", vedi AllowedUserNameCharacters in
    /// AuthenticationExtensions), ma non in testa, in coda o doppi: "Fabio  Iacurto" e
    /// "Fabio Iacurto " sembrerebbero lo stesso nome di un altro utente pur essendo diversi.
    /// </summary>
    public static class RegoleUsername
    {
        public const string MessaggioSpazi =
            "Il nome utente non può iniziare o finire con uno spazio, né contenere due spazi di seguito.";

        public static bool SpaziValidi(string? username) =>
            string.IsNullOrEmpty(username) || (username == username.Trim() && !username.Contains("  "));
    }

    /// <summary>Stesse regole della registrazione per la modifica da Admin (campi facoltativi).</summary>
    public class UpdateUserDTOValidator : AbstractValidator<UpdateUserDTO>
    {
        public UpdateUserDTOValidator()
        {
            RuleFor(x => x.UserName)
                .MinimumLength(3).WithMessage("Il nome utente deve avere almeno 3 caratteri.")
                .MaximumLength(50).WithMessage("Il nome utente non può superare 50 caratteri.")
                .Must(RegoleUsername.SpaziValidi).WithMessage(RegoleUsername.MessaggioSpazi)
                .When(x => !string.IsNullOrWhiteSpace(x.UserName));

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Inserire un indirizzo email valido.")
                .When(x => !string.IsNullOrWhiteSpace(x.Email));
        }
    }
}
