namespace GestoraWebApi.Services.Account.DTOs
{
    /// <summary>V2-019: i dati dell'utente collegato, per la pagina «Il mio account».</summary>
    public class MioAccountDTO
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public IList<string> Ruoli { get; set; } = new List<string>();
        public DateTime DataCreazione { get; set; }
        public DateTime? UltimoAccesso { get; set; }
        public DateTime? PrivacyAccettataIl { get; set; }
        public string? PrivacyVersione { get; set; }
    }

    /// <summary>V2-019: cancellazione dell'account, confermata con la password.</summary>
    public class CancellaAccountDTO
    {
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>V2-019: cambio password dall'account, con quella attuale.</summary>
    public class CambiaPasswordDTO
    {
        public string PasswordAttuale { get; set; } = string.Empty;
        public string NuovaPassword { get; set; } = string.Empty;
    }
}
