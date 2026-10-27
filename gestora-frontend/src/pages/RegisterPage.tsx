import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router-dom'
import apiClient from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Logo } from '@/components/Logo'
import { Input } from '@/components/ui/input'
import { ErroreForm } from '@/components/PageState'
import { SchermataMessaggio } from '@/components/SchermataMessaggio'
import { Label } from '@/components/ui/label'
import { emailSchema, passwordSchema, usernameSchema } from '@/lib/validazioni'
import { erroriPerCampo, messaggioErroreCaricamento } from '@/lib/apiError'

// GAP-001: registrazione pubblica per i clienti — assegna sempre il ruolo Cliente
// (POST /register lato backend non accetta un ruolo diverso).
const schema = z.object({
  username: usernameSchema,
  email: emailSchema,
  password: passwordSchema,
  // V2-019: senza presa visione dell'informativa non ci si registra (il backend lo ricontrolla).
  accettaPrivacy: z.literal(true, {
    error: 'Per registrarti devi dichiarare di aver letto l’informativa privacy.',
  }),
})

type RegisterForm = z.infer<typeof schema>

const CAMPI = ['username', 'email', 'password', 'accettaPrivacy'] as const

export default function RegisterPage() {
  // V2-003: dopo la registrazione non si va al login (senza email confermata non si entra):
  // si resta qui e si dice di controllare la posta.
  const [registrata, setRegistrata] = useState<{ email: string; emailInviata: boolean } | null>(
    null
  )

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<RegisterForm>({ resolver: zodResolver(schema) })

  async function onSubmit(data: RegisterForm) {
    try {
      const response = await apiClient.post(Endpoints.auth.register, data)
      setRegistrata({ email: data.email, emailInviata: response.data?.emailInviata === true })
    } catch (err) {
      // Prima si leggeva solo `message`: gli errori di Identity (email o nome gia' usati)
      // arrivavano in un'altra forma e l'utente vedeva solo "Registrazione non riuscita".
      const { perCampo, altri } = erroriPerCampo(err, CAMPI)
      for (const campo of CAMPI) {
        const messaggio = perCampo[campo]
        if (messaggio) setError(campo, { message: messaggio })
      }
      if (altri.length > 0) {
        setError('root', { message: altri.join(', ') })
      } else if (Object.keys(perCampo).length === 0) {
        setError('root', {
          message: messaggioErroreCaricamento(err, 'Registrazione non riuscita. Riprova.'),
        })
      }
    }
  }

  if (registrata) {
    return (
      <SchermataMessaggio
        titolo="Controlla la tua email"
        tono={registrata.emailInviata ? 'neutro' : 'errore'}
        azioni={
          <Button asChild>
            <Link to="/login">Vai all’accesso</Link>
          </Button>
        }
      >
        {registrata.emailInviata ? (
          <p>
            Ti abbiamo scritto a <strong className="text-foreground">{registrata.email}</strong>.
            Apri il link nel messaggio per attivare l’account, poi potrai accedere. Se non lo
            trovi, guarda anche nella posta indesiderata.
          </p>
        ) : (
          <p>
            L’account è stato creato, ma non siamo riusciti a inviarti l’email di conferma. Prova
            ad accedere: ti proporremo di farti rimandare il link.
          </p>
        )}
      </SchermataMessaggio>
    )
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center gap-6 p-4">
      <Link to="/" aria-label="Torna alla pagina iniziale">
        <Logo className="text-foreground" />
      </Link>
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-titolo">Crea il tuo account</CardTitle>
          <CardDescription className="text-corpo text-muted-foreground">
            Bastano email e password: le prenotazioni le gestisci da qui.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
            <div>
              <Label htmlFor="register-username" className="sr-only">
                Username
              </Label>
              <Input
                id="register-username"
                {...register('username')}
                type="text"
                placeholder="Username"
              />
              {errors.username && (
                <p className="text-nota text-destructive mt-1">{errors.username.message}</p>
              )}
            </div>
            <div>
              <Label htmlFor="register-email" className="sr-only">
                Email
              </Label>
              <Input id="register-email" {...register('email')} type="email" placeholder="Email" />
              {errors.email && (
                <p className="text-nota text-destructive mt-1">{errors.email.message}</p>
              )}
            </div>
            <div>
              <Label htmlFor="register-password" className="sr-only">
                Password
              </Label>
              <Input
                id="register-password"
                {...register('password')}
                type="password"
                placeholder="Password"
              />
              {errors.password && (
                <p className="text-nota text-destructive mt-1">{errors.password.message}</p>
              )}
            </div>
            <div>
              <label className="text-corpo flex items-start gap-2 text-muted-foreground">
                <input
                  id="register-privacy"
                  type="checkbox"
                  className="mt-1 size-4 accent-primary"
                  {...register('accettaPrivacy')}
                />
                <span>
                  Ho letto l’
                  <Link to="/privacy" target="_blank" className="underline underline-offset-4 hover:text-foreground">
                    informativa privacy
                  </Link>{' '}
                  e accetto i{' '}
                  <Link to="/termini" target="_blank" className="underline underline-offset-4 hover:text-foreground">
                    termini di servizio
                  </Link>
                  .
                </span>
              </label>
              {errors.accettaPrivacy && (
                <p className="text-nota text-destructive mt-1">{errors.accettaPrivacy.message}</p>
              )}
            </div>
            <ErroreForm messaggio={errors.root?.message} />
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Creazione…' : 'Crea account'}
            </Button>
          </form>
          <p className="text-corpo mt-4 text-center text-muted-foreground">
            Hai già un account?{' '}
            <Link to="/login" className="underline underline-offset-4 hover:text-foreground">
              Accedi
            </Link>
          </p>
        </CardContent>
      </Card>
      {/* Il logo in alto riporta alla vetrina, ma nessuno lo sa: serve un rimando esplicito. */}
      <Link to="/" className="text-nota text-muted-foreground hover:text-foreground">
        ← Torna alla pagina del locale
      </Link>
      <p className="text-nota text-muted-foreground">
        <Link to="/privacy" className="hover:text-foreground">
          Informativa privacy
        </Link>
        {' · '}
        <Link to="/termini" className="hover:text-foreground">
          Termini di servizio
        </Link>
      </p>
    </div>
  )
}
