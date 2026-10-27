namespace GestoraWebApi.Services.Auth.DTOs
{
    /// <summary>V2-019: richiesta del link per reimpostare la password.</summary>
    public class PasswordDimenticataDTO
    {
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>V2-019: nuova password scelta dal link ricevuto per email.</summary>
    public class ReimpostaPasswordDTO
    {
        public string UserId { get; set; } = string.Empty;

        /// <summary>Token di Identity in Base64 adatto agli URL, come arriva dal link.</summary>
        public string Token { get; set; } = string.Empty;

        public string NuovaPassword { get; set; } = string.Empty;
    }
}
