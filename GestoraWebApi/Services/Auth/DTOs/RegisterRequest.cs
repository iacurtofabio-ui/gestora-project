namespace GestoraWebApi.Services.Auth.DTOs
{
    public class RegisterDTO

    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }

        /// <summary>
        /// V2-019: presa visione dell'informativa privacy, obbligatoria nella registrazione
        /// pubblica. Ignorata quando registra un Admin: l'utente la vedrà al primo accesso.
        /// </summary>
        public bool AccettaPrivacy { get; set; }
    }
}
