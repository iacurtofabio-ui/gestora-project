import { Link } from 'react-router-dom'
import { PaginaLegale } from '@/components/legale/PaginaLegale'
import { EMAIL_PRIVACY, TITOLARE } from '@/lib/privacy'

/**
 * V2-019 — termini di servizio. Le regole sono quelle che Gestora applica davvero (limite online,
 * una prenotazione al giorno, modifica fino a 2 ore prima): se cambiano nel codice o nella
 * configurazione, va cambiato anche questo testo.
 */
export default function TerminiPage() {
  return (
    <PaginaLegale titolo="Termini di servizio">
      <section>
        <p>
          Gestora è il servizio con cui prenoti un tavolo in questo locale. È gestito da {TITOLARE}.
          Creando un account accetti queste regole.
        </p>
      </section>

      <section>
        <h2>Il tuo account</h2>
        <ul>
          <li>Un account per persona, con un’email che usi davvero: serve per confermarlo.</li>
          <li>
            La password è tua: non darla ad altri. Se pensi che qualcuno la conosca, cambiala
            subito.
          </li>
          <li>Puoi cancellare l’account quando vuoi dalla pagina «Il mio account».</li>
        </ul>
      </section>

      <section>
        <h2>Le prenotazioni</h2>
        <ul>
          <li>
            Online si prenota fino al numero di persone indicato nel modulo. Per gruppi più grandi
            contatta direttamente il locale.
          </li>
          <li>Una prenotazione al giorno per account.</li>
          <li>
            Puoi modificare o annullare la prenotazione fino a 2 ore prima del turno. Dopo, contatta
            il locale.
          </li>
          <li>
            Il tavolo lo assegna Gestora in base alle persone e ai posti liberi, nella zona che hai
            scelto se ne hai indicata una.
          </li>
          <li>Se non ti presenti senza avvisare, la prenotazione resta segnata come non presentata.</li>
        </ul>
      </section>

      <section>
        <h2>Uso corretto</h2>
        <p>
          Non usare Gestora per prenotazioni false, per creare account a nome di altri o per
          sovraccaricare il servizio. In questi casi l’account può essere sospeso o eliminato.
        </p>
      </section>

      <section>
        <h2>Disponibilità del servizio</h2>
        <p>
          Facciamo il possibile perché Gestora funzioni sempre, ma può capitare che sia lento o non
          raggiungibile, per esempio durante la manutenzione. Una prenotazione vale quando la vedi
          confermata nelle tue prenotazioni.
        </p>
      </section>

      <section>
        <h2>Privacy e contatti</h2>
        <p>
          Come trattiamo i tuoi dati è scritto nell’<Link to="/privacy">informativa privacy</Link>.
          Per domande scrivi a <a href={`mailto:${EMAIL_PRIVACY}`}>{EMAIL_PRIVACY}</a>.
        </p>
      </section>

      <section>
        <h2>Modifiche</h2>
        <p>
          Se questi termini cambiano in modo sostanziale, te li mostriamo di nuovo al primo
          accesso. Valgono le leggi italiane.
        </p>
      </section>
    </PaginaLegale>
  )
}
