using FluentValidation;
using GestoraWebApi.Services.Auth.DTOs;

namespace GestoraWebApi.Validators
{
    public class PasswordDimenticataDTOValidator : AbstractValidator<PasswordDimenticataDTO>
    {
        public PasswordDimenticataDTOValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("L'email è obbligatoria.")
                .EmailAddress().WithMessage("Inserire un indirizzo email valido.");
        }
    }

    public class ReimpostaPasswordDTOValidator : AbstractValidator<ReimpostaPasswordDTO>
    {
        public ReimpostaPasswordDTOValidator()
        {
            RuleFor(x => x.UserId).NotEmpty().WithMessage("Il link non è valido.");
            RuleFor(x => x.Token).NotEmpty().WithMessage("Il link non è valido.");
            RuleFor(x => x.NuovaPassword).PasswordSicura();
        }
    }
}
