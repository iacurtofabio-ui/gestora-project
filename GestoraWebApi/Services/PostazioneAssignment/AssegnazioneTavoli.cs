using GestoraWebApi.Models;

namespace GestoraWebApi.Services.PostazioneAssignment
{
    /// <summary>
    /// Motore di assegnazione tavoli: logica pura, nessuna dipendenza da database o repository.
    /// Isolata apposta dal service per poter essere testata direttamente.
    /// </summary>
    public static class AssegnazioneTavoli
    {
        /// <summary>
        /// Posti guadagnati sulle testate quando si accostano più tavoli da 2.
        /// Vale solo per unioni composte esclusivamente da tavoli da 2 posti: in ogni altra
        /// combinazione la capienza è la somma semplice.
        /// </summary>
        public const int BonusTestate = 2;

        private const int CapienzaTavoloPiccolo = 2;

        /// <summary>
        /// Capienza reale di un tavolo singolo o di un'unione di tavoli.
        /// </summary>
        public static int CalcolaCapienza(IReadOnlyCollection<Postazione> combinazione)
        {
            if (combinazione == null || combinazione.Count == 0)
                return 0;

            var somma = combinazione.Sum(p => p.CapienzaMassima);

            if (combinazione.Count > 1 && combinazione.All(p => p.CapienzaMassima == CapienzaTavoloPiccolo))
                return somma + BonusTestate;

            return somma;
        }

        /// <summary>
        /// V2-024 — il gruppo più grande che i tavoli liberi possono accogliere: per ogni zona la
        /// capienza dell'unione di tutti i suoi tavoli, poi la zona migliore. Basta l'unione intera
        /// perché aggiungere un tavolo non abbassa mai la capienza (lo stesso motivo del criterio
        /// di arresto). Nessun tavolo libero = 0.
        /// </summary>
        public static int GruppoMassimo(IEnumerable<Postazione> postazioniLibere)
        {
            if (postazioniLibere == null)
                return 0;

            return postazioniLibere
                .GroupBy(p => p.ZonaId)
                .Select(zona => CalcolaCapienza(zona.ToList()))
                .DefaultIfEmpty(0)
                .Max();
        }

        /// <summary>
        /// Trova la migliore assegnazione possibile per i coperti richiesti, valutando insieme
        /// tavoli singoli e unioni di quanti tavoli servono, sempre della stessa zona (V2-007: non c'è
        /// più il limite di 4 tavoli; il gruppo è già limitato dal tetto dei coperti per prenotazione).
        /// Criteri, in ordine: meno posti sprecati, poi meno tavoli occupati.
        /// Restituisce null se nessuna combinazione è sufficiente.
        /// </summary>
        public static List<Postazione>? TrovaMigliorCombinazione(IEnumerable<Postazione> postazioniLibere, int numeroCoperti)
        {
            if (postazioniLibere == null || numeroCoperti <= 0)
                return null;

            List<Postazione>? migliore = null;
            var miglioreSpreco = int.MaxValue;

            // I tavoli si accostano fisicamente: un'unione ha senso solo dentro la stessa zona.
            foreach (var zona in postazioniLibere.GroupBy(p => p.ZonaId).OrderBy(g => g.Key))
            {
                var candidata = TrovaMigliorCombinazioneInZona(zona.ToList(), numeroCoperti, out var spreco);

                if (candidata == null)
                    continue;

                if (migliore == null
                    || spreco < miglioreSpreco
                    || (spreco == miglioreSpreco && candidata.Count < migliore.Count))
                {
                    migliore = candidata;
                    miglioreSpreco = spreco;
                }
            }

            return migliore;
        }

        /// <summary>
        /// Distribuisce i coperti sui tavoli assegnati: ogni tavolo riceve al massimo la propria
        /// capienza, e gli eventuali posti di testata (che non appartengono a nessun tavolo in
        /// particolare) vengono ripartiti sui tavoli dell'unione.
        /// La somma dei posti distribuiti è sempre pari ai coperti richiesti.
        /// </summary>
        public static Dictionary<long, int> DistribuisciCoperti(IReadOnlyList<Postazione> combinazione, int numeroCoperti)
        {
            var distribuzione = combinazione.ToDictionary(p => p.Id, _ => 0);
            var residuo = numeroCoperti;

            // Primo giro: riempio i tavoli più grandi, senza superare la capienza nominale.
            foreach (var p in combinazione.OrderByDescending(p => p.CapienzaMassima).ThenBy(p => p.Id))
            {
                if (residuo <= 0)
                    break;

                var posti = Math.Min(p.CapienzaMassima, residuo);
                distribuzione[p.Id] = posti;
                residuo -= posti;
            }

            // Secondo giro: i posti di testata, uno per tavolo, finché il residuo si esaurisce.
            foreach (var p in combinazione.OrderByDescending(p => p.CapienzaMassima).ThenBy(p => p.Id))
            {
                if (residuo <= 0)
                    break;

                distribuzione[p.Id] += 1;
                residuo -= 1;
            }

            return distribuzione;
        }

        private static List<Postazione>? TrovaMigliorCombinazioneInZona(List<Postazione> postazioniZona, int numeroCoperti, out int sprecoMigliore)
        {
            sprecoMigliore = int.MaxValue;

            // Due tavoli con la stessa capienza sono intercambiabili: ragiono sulle capienze
            // distinte (poche) invece che sui singoli tavoli (potenzialmente molti). Così il
            // numero di combinazioni da valutare non cresce con il numero di tavoli in sala.
            var tavoliPerCapienza = postazioniZona
                .GroupBy(p => p.CapienzaMassima)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Numero).ThenBy(p => p.Id).ToList());

            var capienzeDistinte = tavoliPerCapienza.Keys.ToList();

            List<int>? capienzeMigliori = null;

            foreach (var combinazione in GeneraCombinazioniDiCapienze(capienzeDistinte, tavoliPerCapienza, numeroCoperti))
            {
                var capienza = CalcolaCapienzaDaValori(combinazione);

                if (capienza < numeroCoperti)
                    continue;

                var spreco = capienza - numeroCoperti;

                if (capienzeMigliori == null
                    || spreco < sprecoMigliore
                    || (spreco == sprecoMigliore && combinazione.Count < capienzeMigliori.Count))
                {
                    capienzeMigliori = new List<int>(combinazione);
                    sprecoMigliore = spreco;
                }
            }

            if (capienzeMigliori == null)
                return null;

            // Materializzo: per ogni capienza scelta prendo i tavoli concreti disponibili.
            var selezionati = new List<Postazione>();
            foreach (var gruppo in capienzeMigliori.GroupBy(c => c))
                selezionati.AddRange(tavoliPerCapienza[gruppo.Key].Take(gruppo.Count()));

            return selezionati;
        }

        private static int CalcolaCapienzaDaValori(IReadOnlyCollection<int> capienze)
        {
            var somma = capienze.Sum();

            if (capienze.Count > 1 && capienze.All(c => c == CapienzaTavoloPiccolo))
                return somma + BonusTestate;

            return somma;
        }

        /// <summary>
        /// Genera le combinazioni di capienze (con ripetizione, entro le quantità realmente
        /// disponibili), in ordine crescente di capienza.
        ///
        /// V2-007 — criterio di arresto al posto del vecchio limite di 4 tavoli: una combinazione
        /// che copre già <paramref name="numeroCoperti"/> non viene allungata. È sicuro perché,
        /// generando le capienze in ordine crescente, aggiungere un tavolo non abbassa MAI la
        /// capienza dell'unione, nemmeno col bonus testate (a un'unione di soli tavoli da 2 si
        /// aggiunge solo un tavolo da 2 o più grande): le unioni più lunghe sprecherebbero di più.
        /// Così le combinazioni da valutare restano poche anche con molti tavoli in sala.
        /// </summary>
        private static IEnumerable<List<int>> GeneraCombinazioniDiCapienze(
            List<int> capienzeDistinte,
            Dictionary<int, List<Postazione>> tavoliPerCapienza,
            int numeroCoperti)
        {
            var corrente = new List<int>();

            IEnumerable<List<int>> Ricorsione(int indicePartenza)
            {
                for (var i = indicePartenza; i < capienzeDistinte.Count; i++)
                {
                    var capienza = capienzeDistinte[i];

                    // Non posso usare più tavoli di quanti ne esistono con quella capienza.
                    if (corrente.Count(c => c == capienza) >= tavoliPerCapienza[capienza].Count)
                        continue;

                    corrente.Add(capienza);
                    // Copia difensiva: `corrente` viene mutata dalla ricorsione subito dopo.
                    yield return new List<int>(corrente);

                    if (CalcolaCapienzaDaValori(corrente) < numeroCoperti)
                    {
                        foreach (var risultato in Ricorsione(i))
                            yield return risultato;
                    }

                    corrente.RemoveAt(corrente.Count - 1);
                }
            }

            return Ricorsione(0);
        }
    }
}
