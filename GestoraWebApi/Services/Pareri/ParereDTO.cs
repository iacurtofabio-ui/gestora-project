namespace GestoraWebApi.Services.Pareri
{
    /// <summary>V2-022: un parere dal modulo pubblico /pareri.</summary>
    public class ParereDTO
    {
        public string Messaggio { get; set; } = string.Empty;
        public string? Nome { get; set; }

        /// <summary>Facoltativa: serve solo se chi scrive vuole una risposta.</summary>
        public string? Email { get; set; }

        /// <summary>
        /// Campo trappola: nel modulo è invisibile, quindi una persona lo lascia vuoto. Se arriva
        /// compilato, a scrivere è un programma automatico.
        /// </summary>
        public string? SitoWeb { get; set; }
    }
}
