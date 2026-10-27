using FluentValidation;
using GestoraWebApi.Services.Auth.DTOs;

namespace GestoraWebApi.Validators
{
    // Senza questo validator, il reset password fatto dall'Admin passava solo dalla policy
    // di base di Identity (6 caratteri, un numero) — più debole di quella richiesta in fase di
    // registrazione. Stessa policy di RegisterDTOValidator, per non lasciare una scorciatoia.
    public class AdminResetPasswordDTOValidator : AbstractValidator<AdminResetPasswordDTO>
    {
        public AdminResetPasswordDTOValidator()
        {
            RuleFor(x => x.NewPassword).PasswordSicura();
        }
    }
}
