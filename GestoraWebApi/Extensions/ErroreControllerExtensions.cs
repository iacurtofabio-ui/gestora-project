using GestoraWebApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace GestoraWebApi.Extensions
{
    /// <summary>
    /// Per gli errori che un controller decide da solo, senza lanciare un'eccezione. Il corpo è lo
    /// stesso di <see cref="Infrastructure.Middleware.ExceptionMiddleware"/>: il frontend legge il
    /// motivo da <c>message</c>, e con una stringa semplice l'utente vedrebbe solo una frase generica.
    /// </summary>
    public static class ErroreControllerExtensions
    {
        public static ObjectResult Errore(this ControllerBase controller, int stato, string messaggio)
            => controller.StatusCode(stato, new ErrorDetails { StatusCode = stato, Message = messaggio });
    }
}
