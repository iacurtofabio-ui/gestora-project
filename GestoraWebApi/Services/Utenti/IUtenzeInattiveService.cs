namespace GestoraWebApi.Services.Utenti
{
    public interface IUtenzeInattiveService
    {
        /// <summary>V2-015: elimina un lotto di utenze mai usate. Restituisce quante ne ha eliminate.</summary>
        Task<int> EliminaUtenzeMaiUsateAsync();

        /// <summary>
        /// V2-019: manda l'email di avviso ai Clienti che non entrano da quasi
        /// <c>MesiInattivita</c> mesi. Restituisce quanti ne ha avvisati.
        /// </summary>
        Task<int> AvvisaInattiviAsync();

        /// <summary>
        /// V2-019: elimina i Clienti senza accessi da <c>MesiInattivita</c> mesi e avvisati da almeno
        /// <c>SettimaneAvviso</c> settimane, salvo chi ha prenotazioni in programma. Restituisce quanti.
        /// </summary>
        Task<int> EliminaInattiviAsync();
    }
}
