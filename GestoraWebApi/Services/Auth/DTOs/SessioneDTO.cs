namespace GestoraWebApi.Services.Auth.DTOs
{
    /// <summary>
    /// V2-028 — chi è collegato, per il frontend. Prima lo ricavava leggendo dentro il token; ora
    /// il token sta in un cookie che JavaScript non può leggere, e questi dati arrivano da login,
    /// rinnovo, cambio password e <c>GET api/AuthenticationUser/sessione</c>.
    /// </summary>
    /// <param name="Scadenza">Scadenza del token in UTC: il frontend la usa per rinnovarlo in tempo.</param>
    public record SessioneDTO(string Id, string Email, IList<string> Ruoli, DateTime Scadenza);
}
