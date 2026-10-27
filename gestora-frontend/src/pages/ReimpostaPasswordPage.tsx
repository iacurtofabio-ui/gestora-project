import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useSearchParams } from 'react-router-dom'
import apiClient from '@/lib/axios'
import { Endpoints } from '@/lib/endpoints'
import { passwordSchema } from '@/lib/validazioni'
import { erroriPerCampo, messaggioErroreCaricamento } from '@/lib/apiError'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Logo } from '@/components/Logo'
import { ErroreForm } from '@/components/PageState'
import { SchermataMessaggio } from '@/components/SchermataMessaggio'

const schema = z
  .object({ nuovaPassword: passwordSchema, conferma: z.string() })
  .refine((v) => v.nuovaPassword === v.conferma, {
    path: ['conferma'],
    message: 'Le due password non coincidono.',
  })
type Form = z.infer<typeof schema>

/**
 * V2-019 — pagina a cui porta il link dell'email di recupero (`?userId=…&token=…`). Pubblica:
 * chi la apre non riesce ad accedere. Dopo il cambio le sessioni aperte con la vecchia password
 * si chiudono (il server cambia il security stamp).
 */
export default function ReimpostaPasswordPage() {
  const [params] = useSearchParams()
  const userId = params.get('userId')
  const token = params.get('token')
  const [fatto, setFatto] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<Form>({ resolver: zodResolver(schema) })

  async function onSubmit(data: Form) {
    try {
      await apiClient.post(Endpoints.auth.reimpostaPassword, {
        userId,
        token,
        nuovaPassword: data.nuovaPassword,
      })
      setFatto(true)
    } catch (err) {
      // Gli errori di Identity sulla password arrivano sul campo «password» (ErroriIdentity).
      const { perCampo } = erroriPerCampo(err, ['password', 'nuovaPassword'] as const)
      const sulCampo = perCampo.nuovaPassword ?? perCampo.password
      if (sulCampo) setError('nuovaPassword', { message: sulCampo })
      else
        setError('root', {
          message: messaggioErroreCaricamento(err, 'Password non aggiornata. Riprova.'),
        })
    }
  }

  if (!userId || !token) {
    return (
      <SchermataMessaggio
        titolo="Link non valido"
        azioni={
          <Button asChild>
            <Link to="/password-dimenticata">Chiedi un nuovo link</Link>
          </Button>
        }
      >
        <p>Il link non è completo. Aprilo di nuovo dall’email o chiedine uno nuovo.</p>
      </SchermataMessaggio>
    )
  }

  if (fatto) {
    return (
      <SchermataMessaggio
        titolo="Password aggiornata"
        azioni={
          <Button asChild>
            <Link to="/login">Vai all’accesso</Link>
          </Button>
        }
      >
        <p>Ora puoi accedere con la nuova password.</p>
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
          <CardTitle className="text-titolo">Scegli una nuova password</CardTitle>
          <CardDescription className="text-corpo text-muted-foreground">
            Almeno 8 caratteri, con una maiuscola, un numero e un carattere speciale.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
            <div>
              <Label htmlFor="nuova-password" className="sr-only">
                Nuova password
              </Label>
              <Input
                id="nuova-password"
                {...register('nuovaPassword')}
                type="password"
                placeholder="Nuova password"
                autoComplete="new-password"
              />
              {errors.nuovaPassword && (
                <p className="text-nota text-destructive mt-1">{errors.nuovaPassword.message}</p>
              )}
            </div>
            <div>
              <Label htmlFor="conferma-password" className="sr-only">
                Ripeti la password
              </Label>
              <Input
                id="conferma-password"
                {...register('conferma')}
                type="password"
                placeholder="Ripeti la password"
                autoComplete="new-password"
              />
              {errors.conferma && (
                <p className="text-nota text-destructive mt-1">{errors.conferma.message}</p>
              )}
            </div>
            <ErroreForm messaggio={errors.root?.message} />
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Salvataggio…' : 'Salva la nuova password'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
