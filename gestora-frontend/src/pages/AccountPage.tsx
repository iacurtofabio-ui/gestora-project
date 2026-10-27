import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router-dom'
import { useCambiaPassword, useMioAccount } from '@/hooks/useAccount'
import { passwordSchema } from '@/lib/validazioni'
import { istanteInItalia } from '@/lib/date'
import { erroriPerCampo, messaggioErroreCaricamento } from '@/lib/apiError'
import { IntestazionePagina } from '@/components/IntestazionePagina'
import { CancellaAccountCard } from '@/components/CancellaAccountCard'
import { ErroreForm, PageError, PageLoading } from '@/components/PageState'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const schema = z
  .object({
    passwordAttuale: z.string().min(1, 'Scrivi la password attuale.'),
    nuovaPassword: passwordSchema,
    conferma: z.string(),
  })
  .refine((v) => v.nuovaPassword === v.conferma, {
    path: ['conferma'],
    message: 'Le due password non coincidono.',
  })
type Form = z.infer<typeof schema>

/**
 * V2-019 — «Il mio account», per ogni ruolo: i propri dati, il cambio password e la cancellazione
 * dell'account.
 */
export default function AccountPage() {
  const account = useMioAccount()

  return (
    <div className="space-y-6">
      <IntestazionePagina titolo="Il mio account" />
      {account.isPending ? (
        <PageLoading />
      ) : account.isError ? (
        <PageError
          error={account.error}
          fallback="Dati dell’account non caricati."
          onRiprova={() => account.refetch()}
        />
      ) : (
        <div className="grid gap-6 lg:grid-cols-2">
          <Card>
            <CardHeader>
              <CardTitle className="text-sezione">I tuoi dati</CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="text-corpo grid grid-cols-[auto_1fr] gap-x-6 gap-y-2">
                <dt className="text-muted-foreground">Nome utente</dt>
                <dd>{account.data.userName}</dd>
                <dt className="text-muted-foreground">Email</dt>
                <dd className="break-all">{account.data.email}</dd>
                <dt className="text-muted-foreground">Ruolo</dt>
                <dd>{account.data.ruoli.join(', ')}</dd>
                <dt className="text-muted-foreground">Registrato il</dt>
                <dd>{istanteInItalia(account.data.dataCreazione)}</dd>
                <dt className="text-muted-foreground">Informativa</dt>
                <dd>
                  {account.data.privacyAccettataIl
                    ? `letta il ${istanteInItalia(account.data.privacyAccettataIl)}`
                    : 'non ancora letta'}{' '}
                  ·{' '}
                  <Link to="/privacy" target="_blank" className="underline underline-offset-4">
                    rileggi
                  </Link>
                </dd>
              </dl>
            </CardContent>
          </Card>
          <CambiaPasswordCard />
          <CancellaAccountCard />
        </div>
      )}
    </div>
  )
}

function CambiaPasswordCard() {
  const cambia = useCambiaPassword()
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
    setError,
  } = useForm<Form>({ resolver: zodResolver(schema) })

  function onSubmit(data: Form) {
    cambia.mutate(
      { passwordAttuale: data.passwordAttuale, nuovaPassword: data.nuovaPassword },
      {
        onSuccess: () => reset(),
        onError: (err) => {
          // Gli errori di Identity sulla nuova password arrivano sul campo «password».
          const { perCampo, altri } = erroriPerCampo(err, ['passwordAttuale', 'nuovaPassword', 'password'] as const)
          if (perCampo.passwordAttuale) setError('passwordAttuale', { message: perCampo.passwordAttuale })
          const sullaNuova = perCampo.nuovaPassword ?? perCampo.password
          if (sullaNuova) setError('nuovaPassword', { message: sullaNuova })
          if (!perCampo.passwordAttuale && !sullaNuova)
            setError('root', {
              message: altri[0] ?? messaggioErroreCaricamento(err, 'Password non cambiata. Riprova.'),
            })
        },
      }
    )
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sezione">Cambia password</CardTitle>
        <CardDescription className="text-corpo text-muted-foreground">
          Almeno 8 caratteri, con una maiuscola, un numero e un carattere speciale. Le altre
          sessioni aperte verranno chiuse.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
          <div className="space-y-1">
            <Label htmlFor="password-attuale">Password attuale</Label>
            <Input
              id="password-attuale"
              type="password"
              autoComplete="current-password"
              {...register('passwordAttuale')}
            />
            {errors.passwordAttuale && (
              <p className="text-nota text-destructive">{errors.passwordAttuale.message}</p>
            )}
          </div>
          <div className="space-y-1">
            <Label htmlFor="password-nuova">Nuova password</Label>
            <Input id="password-nuova" type="password" autoComplete="new-password" {...register('nuovaPassword')} />
            {errors.nuovaPassword && (
              <p className="text-nota text-destructive">{errors.nuovaPassword.message}</p>
            )}
          </div>
          <div className="space-y-1">
            <Label htmlFor="password-conferma">Ripeti la nuova password</Label>
            <Input id="password-conferma" type="password" autoComplete="new-password" {...register('conferma')} />
            {errors.conferma && <p className="text-nota text-destructive">{errors.conferma.message}</p>}
          </div>
          <ErroreForm messaggio={errors.root?.message} />
          <Button type="submit" disabled={cambia.isPending} className="self-start">
            {cambia.isPending ? 'Salvataggio…' : 'Cambia password'}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
