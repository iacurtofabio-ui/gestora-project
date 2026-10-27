import { Link } from 'react-router-dom'
import { PaginaLegale } from '@/components/legale/PaginaLegale'
import { EMAIL_PRIVACY, TITOLARE } from '@/lib/privacy'

/**
 * V2-019 — informativa privacy (art. 13 GDPR). Descrive i dati che Gestora tratta davvero: se il
 * codice cambia cosa salva o per quanto lo tiene (job di pulizia, registro attività, fornitori),
 * questo testo va aggiornato insieme a INFORMATIVA_VERSIONE in lib/privacy.ts.
 */
export default function PrivacyPage() {
  return (
    <PaginaLegale titolo="Informativa privacy">
      <section>
        <p>
          Questa pagina spiega quali dati personali raccoglie Gestora, il servizio di prenotazione
          di questo locale, perché li usa, per quanto li conserva e come puoi esercitare i tuoi
          diritti.
        </p>
      </section>

      <section>
        <h2>Chi tratta i tuoi dati</h2>
        <p>
          Il titolare del trattamento è {TITOLARE}. Per qualsiasi domanda o richiesta sui tuoi
          dati scrivi a <a href={`mailto:${EMAIL_PRIVACY}`}>{EMAIL_PRIVACY}</a>.
        </p>
      </section>

      <section>
        <h2>Quali dati raccogliamo</h2>
        <ul>
          <li>
            <strong>Account</strong>: nome utente, email, password (salvata solo in forma cifrata,
            nessuno può leggerla), ruolo, data di registrazione, data dell’ultimo accesso, data e
            versione dell’informativa che hai letto.
          </li>
          <li>
            <strong>Prenotazioni</strong>: giorno, turno, numero di persone, note che scrivi, tavoli
            assegnati e, se la prenotazione è presa dal personale, il nome di chi si presenta.
          </li>
          <li>
            <strong>Registro attività</strong>: le operazioni fatte con l’account (registrazione,
            accesso, prenotazioni, modifiche fatte dal personale) con data, ora e indirizzo IP.
          </li>
          <li>
            <strong>Verifica della disponibilità senza account</strong>: l’indirizzo IP serve solo a
            limitare le richieste troppo frequenti e non viene salvato.
          </li>
          <li>
            <strong>Pareri</strong>: il testo che scrivi nel modulo «Lascia un parere» e, solo se li
            indichi, il tuo nome e la tua email. L’indirizzo IP serve solo a limitare gli invii
            troppo frequenti e non viene salvato.
          </li>
        </ul>
      </section>

      <section>
        <h2>Perché li usiamo</h2>
        <ul>
          <li>
            Gestire il tuo account e le tue prenotazioni, e mandarti le email di servizio (conferma
            dell’indirizzo, recupero della password, avviso prima di eliminare un account inattivo).
            Base giuridica: il servizio che hai chiesto (art. 6.1.b GDPR).
          </li>
          <li>
            Tenere il servizio sicuro: registro attività, limiti alle richieste, blocco dopo troppi
            tentativi di accesso sbagliati. Base giuridica: legittimo interesse a proteggere il
            servizio e chi lo usa (art. 6.1.f GDPR).
          </li>
          <li>
            Leggere i pareri e, se hai lasciato l’email, risponderti. Base giuridica: la tua
            richiesta, quando ci scrivi (art. 6.1.b GDPR).
          </li>
        </ul>
        <p>Non usiamo i dati per pubblicità, non li vendiamo e non li cediamo ad altri.</p>
      </section>

      <section>
        <h2>Per quanto li conserviamo</h2>
        <ul>
          <li>Prenotazioni: eliminate 6 mesi dopo il giorno della prenotazione.</li>
          <li>Registro attività: eliminato dopo 24 mesi.</li>
          <li>Account registrato e mai usato: eliminato dopo 180 giorni.</li>
          <li>
            Account senza accessi per 24 mesi: eliminato, dopo un’email di avviso mandata 4
            settimane prima. Basta accedere per tenerlo.
          </li>
          <li>
            Pareri: non vengono salvati in Gestora. Arrivano via email al titolare e restano nella
            sua casella finché servono per rispondere o per migliorare il servizio.
          </li>
          <li>
            Account che cancelli tu: eliminato subito. Le prenotazioni già passate restano per i
            conteggi del locale, senza il tuo nome né il legame con te.
          </li>
        </ul>
      </section>

      <section>
        <h2>Chi ci aiuta a gestirli</h2>
        <p>
          I dati stanno su servizi di fornitori che li trattano per nostro conto, solo per far
          funzionare Gestora:
        </p>
        <ul>
          <li>Microsoft Azure: il server dell’applicazione (Canada).</li>
          <li>Neon: il database (Stati Uniti, su server Amazon Web Services).</li>
          <li>Vercel: il sito che apri nel browser.</li>
          <li>Brevo: l’invio delle email.</li>
        </ul>
        <p>
          Se un fornitore tratta dati fuori dall’Unione europea, il trasferimento avviene con le
          garanzie previste dal GDPR: una decisione di adeguatezza della Commissione europea (per
          il Canada, e per gli Stati Uniti verso le aziende aderenti al Data Privacy Framework) o le
          clausole contrattuali standard.
        </p>
      </section>

      <section>
        <h2>Cookie e memoria del browser</h2>
        <p>
          Gestora non usa cookie di profilazione né di terze parti. Nel browser salva solo due cose
          tecniche: il codice di accesso, finché non esci, e la scelta del tema chiaro o scuro.
        </p>
      </section>

      <section>
        <h2>I tuoi diritti</h2>
        <p>
          Puoi chiedere di vedere i tuoi dati, correggerli, cancellarli, riceverli in un file o
          opporti al loro uso. Dalla pagina «Il mio account» puoi cancellare l’account da solo;
          per tutto il resto, compresa una copia dei tuoi dati, scrivi a{' '}
          <a href={`mailto:${EMAIL_PRIVACY}`}>{EMAIL_PRIVACY}</a>. Se pensi che i tuoi dati siano
          trattati in modo scorretto puoi fare reclamo al{' '}
          <a href="https://www.garanteprivacy.it" target="_blank" rel="noreferrer">
            Garante per la protezione dei dati personali
          </a>
          .
        </p>
      </section>

      <section>
        <h2>Modifiche</h2>
        <p>
          Se questa informativa cambia in modo sostanziale, te la mostriamo di nuovo al primo
          accesso. Le regole d’uso del servizio sono nei{' '}
          <Link to="/termini">termini di servizio</Link>.
        </p>
      </section>
    </PaginaLegale>
  )
}
