import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router-dom'
import apiClient from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'
import { erroriPerCampo, eTroppeRichieste, messaggioErroreCaricamento } from '@/lib/apiError'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Logo } from '@/components/Logo'
import { ErroreForm } from '@/components/PageState'
import { SchermataMessaggio } from '@/components/SchermataMessaggio'

/** Come ParereDTOValidator.MessaggioMassimo nel backend. */
const MESSAGGIO_MASSIMO = 2000

const schema = z.object({
  messaggio: z
    .string()
    .trim()
    .min(1, 'Scrivi il tuo parere.')
    .max(MESSAGGIO_MASSIMO, `Il parere può avere al massimo ${MESSAGGIO_MASSIMO} caratteri.`),
  nome: z.string().trim().max(100, 'Il nome può avere al massimo 100 caratteri.'),
  email: z.union([z.literal(''), z.email({ error: 'Inserire un indirizzo email valido.' })]),
  // Campo trappola: invisibile, una persona lo lascia vuoto.
  sitoWeb: z.string(),
})
type Form = z.infer<typeof schema>

const CAMPI = ['messaggio', 'nome', 'email'] as const

/**
 * V2-022 — modulo pubblico dei pareri. Chiunque può scrivere, con o senza account; il parere
 * arriva via email a chi gestisce Gestora e non si salva. Contro gli invii automatici: limite di
 * richieste nel backend e il campo `sitoWeb`, nascosto alle persone ma compilato dai programmi.
 */
export default function PareriPage() {
  const [inviato, setInviato] = useState(false)
  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<Form>({
    resolver: zodResolver(schema),
    defaultValues: { messaggio: '', nome: '', email: '', sitoWeb: '' },
  })
  const lunghezza = useWatch({ control, name: 'messaggio' }).length

  async function onSubmit(data: Form) {
    try {
      await apiClient.post(Endpoints.pareri.invia, {
        messaggio: data.messaggio,
        nome: data.nome || null,
        email: data.email || null,
        sitoWeb: data.sitoWeb || null,
      })
      setInviato(true)
    } catch (err) {
      // Limite di 2 pareri al mese per indirizzo: «riprova» qui non avrebbe senso.
      if (eTroppeRichieste(err)) {
        setError('root', {
          message: 'Hai già lasciato i tuoi pareri per questo mese, grazie! Potrai scriverne un altro più avanti.',
        })
        return
      }
      const { perCampo } = erroriPerCampo(err, CAMPI)
      for (const campo of CAMPI) {
        if (perCampo[campo]) setError(campo, { message: perCampo[campo] })
      }
      if (Object.keys(perCampo).length === 0)
        setError('root', { message: messaggioErroreCaricamento(err, 'Parere non inviato. Riprova.') })
    }
  }

  if (inviato) {
    return (
      <SchermataMessaggio
        titolo="Grazie per il tuo parere"
        azioni={
          <Button asChild>
            <Link to="/">Torna alla pagina del locale</Link>
          </Button>
        }
      >
        <p>L’abbiamo ricevuto. Se hai lasciato l’email, potremmo risponderti lì.</p>
      </SchermataMessaggio>
    )
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center gap-6 p-4">
      <Link to="/" aria-label="Torna alla pagina iniziale">
        <Logo className="text-foreground" />
      </Link>
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-titolo">Lascia un parere</CardTitle>
          <CardDescription className="text-corpo text-muted-foreground">
            Cosa ti è piaciuto, cosa miglioreresti, un’idea per Gestora: lo leggiamo volentieri.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4" noValidate>
            <div className="space-y-1">
              <Label htmlFor="parere-messaggio">Il tuo parere</Label>
              <textarea
                id="parere-messaggio"
                rows={6}
                maxLength={MESSAGGIO_MASSIMO}
                className="text-corpo w-full rounded-md border border-input bg-transparent px-3 py-2 shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
                {...register('messaggio')}
              />
              <div className="flex justify-between gap-2">
                <p className="text-nota text-destructive">{errors.messaggio?.message}</p>
                <p className="text-nota text-muted-foreground tabular-nums">
                  {lunghezza}/{MESSAGGIO_MASSIMO}
                </p>
              </div>
            </div>
            <div className="space-y-1">
              <Label htmlFor="parere-nome">Nome (facoltativo)</Label>
              <Input id="parere-nome" autoComplete="name" {...register('nome')} />
              {errors.nome && <p className="text-nota text-destructive">{errors.nome.message}</p>}
            </div>
            <div className="space-y-1">
              <Label htmlFor="parere-email">Email (facoltativa, solo se vuoi una risposta)</Label>
              <Input id="parere-email" type="email" autoComplete="email" {...register('email')} />
              {errors.email && <p className="text-nota text-destructive">{errors.email.message}</p>}
            </div>
            {/* Campo trappola: fuori dallo schermo, saltato dal tabulatore e dai lettori di schermo. */}
            <div aria-hidden="true" className="absolute -left-[9999px] h-px w-px overflow-hidden">
              <label htmlFor="parere-sito">Sito web</label>
              <input id="parere-sito" type="text" tabIndex={-1} autoComplete="off" {...register('sitoWeb')} />
            </div>
            <ErroreForm messaggio={errors.root?.message} />
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Invio…' : 'Invia il parere'}
            </Button>
            <p className="text-nota text-muted-foreground">
              Il parere arriva via email a chi gestisce Gestora e non viene salvato nell’applicazione.
              Dettagli nell’{' '}
              <Link to="/privacy" className="underline underline-offset-4 hover:text-foreground">
                informativa privacy
              </Link>
              .
            </p>
          </form>
        </CardContent>
      </Card>
      <Link to="/" className="text-nota text-muted-foreground hover:text-foreground">
        ← Torna alla pagina del locale
      </Link>
    </div>
  )
}
