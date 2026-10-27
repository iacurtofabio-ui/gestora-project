using FluentValidation;
using GestoraWebApi.Services.Account.DTOs;

namespace GestoraWebApi.Validators
{
    public class CambiaPasswordDTOValidator : AbstractValidator<CambiaPasswordDTO>
    {
        public CambiaPasswordDTOValidator()
        {
            RuleFor(x => x.PasswordAttuale).NotEmpty().WithMessage("Scrivi la password attuale.");
            RuleFor(x => x.NuovaPassword)
                .PasswordSicura()
                .NotEqual(x => x.PasswordAttuale).WithMessage("La nuova password deve essere diversa da quella attuale.");
        }
    }

    public class CancellaAccountDTOValidator : AbstractValidator<CancellaAccountDTO>
    {
        public CancellaAccountDTOValidator()
        {
            RuleFor(x => x.Password).NotEmpty().WithMessage("Scrivi la tua password per confermare.");
        }
    }
}
