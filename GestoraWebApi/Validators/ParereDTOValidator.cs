using FluentValidation;
using GestoraWebApi.Services.Pareri;

namespace GestoraWebApi.Validators
{
    public class ParereDTOValidator : AbstractValidator<ParereDTO>
    {
        public const int MessaggioMassimo = 2000;

        public ParereDTOValidator()
        {
            RuleFor(x => x.Messaggio)
                .Must(m => !string.IsNullOrWhiteSpace(m)).WithMessage("Scrivi il tuo parere.")
                .MaximumLength(MessaggioMassimo).WithMessage($"Il parere può avere al massimo {MessaggioMassimo} caratteri.");

            RuleFor(x => x.Nome)
                .MaximumLength(100).WithMessage("Il nome può avere al massimo 100 caratteri.");

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Inserire un indirizzo email valido.")
                .MaximumLength(256).WithMessage("L'email può avere al massimo 256 caratteri.")
                .When(x => !string.IsNullOrWhiteSpace(x.Email));
        }
    }
}
