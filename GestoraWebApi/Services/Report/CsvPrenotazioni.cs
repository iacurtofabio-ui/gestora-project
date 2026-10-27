using System.Globalization;
using System.Text;
using GestoraWebApi.Enums;
using GestoraWebApi.Models;

namespace GestoraWebApi.Services.Report
{
    /// <summary>
    /// V2-025 — il file CSV del report delle prenotazioni, pensato per aprirsi con un doppio clic in
    /// Excel in italiano: separatore «;» (la virgola è il separatore dei decimali), UTF-8 con BOM
    /// (senza, Excel sbaglia gli accenti), righe CRLF. Puro: riceve le prenotazioni già lette.
    /// </summary>
    public static class CsvPrenotazioni
    {
        public const char Separatore = ';';

        public static readonly string[] Intestazione =
            ["Data", "Giorno", "Turno", "Persone", "Cliente", "Stato", "Tavoli", "Zona", "Note"];

        private static readonly CultureInfo Italiano = new("it-IT");

        public static byte[] Componi(IEnumerable<Prenotazione> prenotazioni)
        {
            var testo = new StringBuilder();
            testo.Append(Riga(Intestazione));

            foreach (var p in prenotazioni)
            {
                var tavoli = (p.PrenotazioniPostazioni ?? Enumerable.Empty<PrenotazionePostazione>())
                    .Where(pp => pp.Postazione != null)
                    .Select(pp => pp.Postazione)
                    .OrderBy(t => t.Numero)
                    .ToList();

                testo.Append(Riga(
                [
                    p.DataPrenotazione.ToString("dd/MM/yyyy", Italiano),
                    Italiano.DateTimeFormat.GetDayName(p.DataPrenotazione.DayOfWeek),
                    p.FasciaOraria == null ? "" : $"{p.FasciaOraria.OrarioInizio:HH\\:mm}-{p.FasciaOraria.OrarioFine:HH\\:mm}",
                    p.NumeroCoperti.ToString(Italiano),
                    // Chi si presenta: il nome scritto dallo Staff, altrimenti l'utente che ha prenotato.
                    p.NomeCliente ?? p.User?.UserName ?? "",
                    Stato(p.Stato),
                    string.Join(", ", tavoli.Select(t => t.Numero.ToString(Italiano))),
                    string.Join(", ", tavoli.Select(t => t.Zona?.Nome).Where(z => z != null).Distinct()),
                    p.Note ?? ""
                ]));
            }

            // Il BOM in testa dice a Excel che il file è UTF-8.
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(testo.ToString())).ToArray();
        }

        public static string Stato(StatoPrenotazione stato) => stato switch
        {
            StatoPrenotazione.Attiva => "Attiva",
            StatoPrenotazione.InCorso => "In corso",
            StatoPrenotazione.Completata => "Completata",
            StatoPrenotazione.Annullata => "Annullata",
            StatoPrenotazione.NonPresentata => "Non presentata",
            _ => stato.ToString()
        };

        private static string Riga(IEnumerable<string> celle) =>
            string.Join(Separatore, celle.Select(Cella)) + "\r\n";

        /// <summary>
        /// Una cella sicura: tra virgolette se contiene separatore, virgolette o a capo; con un apice
        /// davanti se comincia come una formula (=, +, -, @), così Excel non esegue ciò che un cliente
        /// ha scritto nel nome o nelle note.
        /// </summary>
        public static string Cella(string valore)
        {
            if (valore.Length > 0 && "=+-@\t\r".Contains(valore[0]))
                valore = "'" + valore;

            return valore.IndexOfAny([Separatore, '"', '\n', '\r']) >= 0
                ? "\"" + valore.Replace("\"", "\"\"") + "\""
                : valore;
        }
    }
}
