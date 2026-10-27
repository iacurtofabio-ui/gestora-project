using GestoraWebApi.Auth;
using GestoraWebApi.Enums;
using GestoraWebApi.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GestoraWebApi.Context
{
    public class GestoraContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {

        public DbSet<Logging> LogActivities { get; set; }
        public DbSet<Postazione> Postazioni { get; set; }
        public DbSet<FasciaOraria> FasciaOrarie { get; set; }
        public DbSet<Prenotazione> Prenotazioni { get; set; }
        public DbSet<PrenotazionePostazione> PrenotazioniPostazioni { get; set; }
        public DbSet<Zona> Zone { get; set; }

        // V2-018: chiavi di Data Protection (firmano i link di conferma email). Vedi
        // DataProtectionExtensions.
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }



        public GestoraContext(DbContextOptions<GestoraContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>().ToTable("Utenti");

            // V2-015: rete di sicurezza. Se un punto che crea utenti dimentica DataCreazione, il
            // database scrive l'istante dell'inserimento invece di 0001-01-01, che per la pulizia
            // notturna sarebbe un'utenza vecchia di duemila anni, da eliminare subito.
            // Vale anche per le righe gia' presenti quando si applica la migration.
            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.DataCreazione)
                .HasDefaultValueSql("now()");

            // V2-019: la versione dell'informativa è un'etichetta corta (es. «2026-11»).
            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.PrivacyVersione)
                .HasMaxLength(20);
            modelBuilder.Entity<IdentityRole>().ToTable("Ruoli");

            //POSTAZIONI
            modelBuilder.Entity<Postazione>(entity =>
            {
                entity.ToTable("Postazioni");

                entity.HasKey(p => p.Id).HasName("Postazione_Pkey");

                entity.Property(p => p.Numero)
                    .IsRequired()
                    .HasColumnName("Numero");

                entity.Property(p => p.CapienzaMassima)
                    .IsRequired()
                    .HasColumnName("CapienzaMassima");

                //entity.Property(p => p.Zona)
                //    .IsRequired()
                //    .HasColumnName("Zona");

                entity.Property(p => p.Attiva)
                    .IsRequired()
                    .HasColumnName("Attiva");                
            });

            //FASCE ORARIE
            modelBuilder.Entity<FasciaOraria>(entity =>
            {
                entity.ToTable("FasceOrarie");

                entity.HasKey(f => f.Id).HasName("FasciaOraria_Pkey");

                entity.Property(f => f.OrarioInizio)
                    .IsRequired()
                    .HasColumnName("OrarioInizio");

                entity.Property(f => f.OrarioFine)
                    .IsRequired()
                    .HasColumnName("OrarioFine");

                // mappo l'enum DayOfWeek come int su DB
                entity.Property(f => f.GiornoSettimana)
                    .HasConversion<int>()
                    .IsRequired()
                    .HasColumnName("GiornoSettimana");

                entity.Property(f => f.MaxCoperti)
                    .IsRequired()
                    .HasColumnName("MaxCoperti");

                entity.Property(f => f.Attiva)
                    .IsRequired()
                    .HasColumnName("Attiva");
            });

            //PRENOTAZIONI

            modelBuilder.Entity<Prenotazione>(entity =>
            {
                entity.ToTable("Prenotazioni");

                entity.HasKey(p => p.Id).HasName("Prenotazione_Pkey");

                entity.Property(p => p.DataPrenotazione)
                    .IsRequired()
                    .HasColumnName("DataPrenotazione");

                entity.Property(p => p.NumeroCoperti)
                    .IsRequired()
                    .HasColumnName("NumeroCoperti");

                entity.Property(p => p.Stato)
                    .IsRequired()
                    .HasColumnName("Stato")
                    .HasConversion<string>();

                entity.Property(p => p.Note)
                    .HasColumnName("Note");

                entity.Property(p => p.NomeCliente)
                    .HasColumnName("NomeCliente")
                    .HasMaxLength(200);

                // REV-038: era Cascade, cioe' eliminare un utente cancellava in silenzio tutto
                // il suo storico di prenotazioni. Lo storico non appartiene solo all'utente: e'
                // il dato su cui si contano coperti, presenze e mancate presentazioni, quindi
                // cancellarlo falsa a ritroso numeri gia' letti e non e' recuperabile.
                // Con Restrict il database rifiuta l'eliminazione finche' esistono prenotazioni,
                // e l'API la traduce in un messaggio esplicito (vedi DeleteUser): l'Admin sa
                // perche' non puo' procedere, invece di distruggere dati senza accorgersene.
                // V2-019: il legame e' facoltativo. Chi cancella il proprio account stacca prima
                // le prenotazioni passate (UserId a null, nome reso anonimo); Restrict resta la
                // rete di sicurezza per chi elimina un utente senza farlo.
                entity.HasOne(p => p.User)
                    .WithMany(u => u.Prenotazioni)
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Restrict);


                entity.HasOne(p => p.FasciaOraria)
                    .WithMany(f => f.Prenotazioni)
                    .HasForeignKey(p => p.FasciaOrariaId);

                // Vincolo "una prenotazione attiva al giorno" rimosso dal DB: Staff/Admin creano
                // prenotazioni per conto di clienti diversi sotto il proprio UserId, quindi un
                // indice univoco su (UserId, Data) li bloccherebbe. Il vincolo ora vive solo in
                // PrenotazioniService, applicato esclusivamente al self-service Cliente
                // (GuardUnaPrenotazioneAlGiornoAsync).

            });

            //PRENOTAZIONE POSTAZIONE
            modelBuilder.Entity<PrenotazionePostazione>(entity =>
            {
                entity.ToTable("PrenotazioniPostazioni");

                entity.HasKey(pp => new { pp.PostazioneId, pp.PrenotazioneId })
                      .HasName("PrenotazionePostazione_Pkey");

                entity.HasOne(pp => pp.Postazione)
                    .WithMany(p => p.PrenotazioniPostazioni)
                    .HasForeignKey(pp => pp.PostazioneId);

                entity.HasOne(pp => pp.Prenotazione)
                    .WithMany(pr => pr.PrenotazioniPostazioni)
                    .HasForeignKey(pp => pp.PrenotazioneId);

                entity.Property(pp => pp.NumeroPosti)                    
                    .HasColumnName("NumeroPosti");

                entity.Property(pp => pp.DataPrenotazione)
                    .IsRequired()
                    .HasColumnName("DataPrenotazione");

                entity.Property(pp => pp.FasciaOrariaId)
                    .IsRequired()
                    .HasColumnName("FasciaOrariaId");

                // REV-003: e' il database a impedire che lo stesso tavolo finisca in due
                // prenotazioni nello stesso slot. Indice PIENO, senza filtro: le righe di una
                // prenotazione annullata vengono cancellate (AnnullaPrenotazioneAsync), quindi
                // non c'e' nulla da escludere con un WHERE.
                entity.HasIndex(pp => new { pp.PostazioneId, pp.DataPrenotazione, pp.FasciaOrariaId })
                    .IsUnique()
                    .HasDatabaseName("UX_PrenotazionePostazione_Slot");

            });

            //ZONA
            modelBuilder.Entity<Zona>(entity =>
            {
                entity.ToTable("Zone");

                entity.HasKey(z => z.Id).HasName("Zona_Pkey");

                entity.Property(z => z.Nome)
                    .IsRequired()
                    .HasColumnName("Nome");
                    

                entity.Property(z => z.Attiva)
                    .IsRequired()
                    .HasColumnName("Attiva");

                entity.HasMany(z => z.Postazioni)
                    .WithOne(p => p.Zona)
                    .HasForeignKey(p => p.ZonaId);
            });

            //AUDIT LOG
            // REV-037: la tabella non aveva ne' indici ne' limiti di lunghezza. E' l'unica che
            // cresce a ogni singola operazione dell'applicazione e non viene mai ripulita:
            // qualunque domanda ("cosa e' successo ieri", "cosa ha fatto questo utente") si
            // risolveva leggendola tutta. Le colonne restano testo libero senza tetto, quindi
            // una stringa anomala poteva occupare spazio senza alcun limite.
            modelBuilder.Entity<Logging>(entity =>
            {
                entity.ToTable("LogActivities");

                entity.Property(l => l.UserId)
                    .IsRequired()
                    .HasMaxLength(450);   // stessa lunghezza della chiave di Identity

                entity.Property(l => l.Action)
                    .IsRequired()
                    .HasMaxLength(500);

                // Un IPv6 con scope arriva a 45-46 caratteri: 45 e' il massimo utile.
                entity.Property(l => l.IPAddress)
                    .HasMaxLength(45);

                // Le due interrogazioni reali sono "gli ultimi eventi" e "gli eventi di un
                // utente, dal piu' recente". Il secondo indice e' composto proprio per servire
                // anche l'ordinamento, non solo il filtro.
                entity.HasIndex(l => l.Timestamp)
                      .HasDatabaseName("IX_LogActivities_Timestamp");

                entity.HasIndex(l => new { l.UserId, l.Timestamp })
                      .HasDatabaseName("IX_LogActivities_UserId_Timestamp");
            });
        }

    }
}
