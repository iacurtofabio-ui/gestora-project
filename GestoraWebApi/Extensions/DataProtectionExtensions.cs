using GestoraWebApi.Context;
using Microsoft.AspNetCore.DataProtection;

namespace GestoraWebApi.Extensions
{
    public static class DataProtectionExtensions
    {
        // Le chiavi di Data Protection firmano i token di Identity: oggi il link di conferma
        // email, con la F3 anche il recupero password. Senza questa configurazione stanno nel
        // filesystem del container, che su Azure si azzera a ogni riavvio: i link gia' inviati
        // smettevano di funzionare prima delle loro 24 ore.
        //
        // Il nome dell'applicazione e' fisso perche' il default dipende dal percorso di
        // installazione: cambiandolo, le chiavi salvate non sarebbero piu' riconosciute.
        public const string NomeApplicazione = "Gestora";

        // V2-018 — chiavi salvate nel database (tabella DataProtectionKeys), cosi' sopravvivono
        // ai riavvii e seguono i backup di Neon. Restano in chiaro nel database: chi le legge
        // ha gia' accesso a tutti i dati, e cifrarle richiederebbe un certificato da custodire
        // altrove. Per questo all'avvio il log avvisa «No XML encryptor configured»: e' atteso.
        public static IServiceCollection AddChiaviDataProtection(this IServiceCollection services)
        {
            services.AddDataProtection()
                .SetApplicationName(NomeApplicazione)
                .PersistKeysToDbContext<GestoraContext>();

            return services;
        }
    }
}
