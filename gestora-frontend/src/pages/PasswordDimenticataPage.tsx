import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router-dom'
import apiClient from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'
import { emailSchema } from '@/lib/validazioni'
import { messaggioErroreCaricamento } from '@/lib/apiError'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Logo } from '@/components/Logo'
import { ErroreForm } from '@/components/PageState'
import { SchermataMessaggio } from '@/components/SchermataMessaggio'

const schema = z.object({ email: emailSchema })
type Form = z.infer<typeof schema>

/**
 * V2-019 — richiesta del link per scegliere una nuova password. Il server risponde sempre allo
 * stesso modo, esista o no l'email: la pagina dice lo stesso, per non rivelare chi è registrato.
 */
export default function PasswordDimenticataPage() {
  const [inviata, setInviata] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<Form>({ resolver: zodResolver(schema) })

  async function onSubmit(data: Form) {
    try {
      await apiClient.post(Endpoints.auth.passwordDimenticata, data)
      setInviata(data.email)
    } catch (err) {
      setError('root', { message: messaggioErroreCaricamento(err, 'Richiesta non riuscita. Riprova.') })
    }
  }

  if (inviata) {
    return (
      <SchermataMessaggio
        titolo="Controlla la tua email"
        azioni={
          <Button asChild>
            <Link to="/login">Torna all’accesso</Link>
          </Button>
        }
      >
        <p>
          Se <strong>{inviata}</strong> è registrata, riceverai un’email con il link per scegliere
          una nuova password. Il link vale 2 ore.
        </p>
        <p>Non la trovi? Guarda nella cartella spam.</p>
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
          <CardTitle className="text-titolo">Password dimenticata</CardTitle>
          <CardDescription className="text-corpo text-muted-foreground">
            Scrivi l’email del tuo account: ti mandiamo un link per sceglierne una nuova.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
            <div>
              <Label htmlFor="recupero-email" className="sr-only">
                Email
              </Label>
              <Input id="recupero-email" {...register('email')} type="email" placeholder="Email" />
              {errors.email && <p className="text-nota text-destructive mt-1">{errors.email.message}</p>}
            </div>
            <ErroreForm messaggio={errors.root?.message} />
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Invio…' : 'Mandami il link'}
            </Button>
          </form>
          <p className="text-corpo mt-4 text-center text-muted-foreground">
            Te la sei ricordata?{' '}
            <Link to="/login" className="underline underline-offset-4 hover:text-foreground">
              Accedi
            </Link>
          </p>
        </CardContent>
      </Card>
    </div>
  )
}
