using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GestoraWebApi.Infrastructure.Auth
{
    /// <summary>
    /// V2-019 — token del link per reimpostare la password, con una durata sua. Il provider di
    /// default vale 24 ore per tutti i token (va bene per la conferma dell'email): un link che
    /// cambia la password deve scadere prima, perché chi legge la posta di qualcun altro avrebbe
    /// meno tempo per usarlo. Le chiavi sono le stesse (Data Protection nel database, V2-018).
    /// </summary>
    public class RecuperoPasswordTokenProvider<TUser> : DataProtectorTokenProvider<TUser> where TUser : class
    {
        public const string Nome = "RecuperoPassword";

        /// <summary>Durata del link, ripetuta nel testo dell'email.</summary>
        public static readonly TimeSpan Durata = TimeSpan.FromHours(2);

        public RecuperoPasswordTokenProvider(IDataProtectionProvider dataProtectionProvider,
                                             IOptions<RecuperoPasswordTokenProviderOptions> options,
                                             ILogger<DataProtectorTokenProvider<TUser>> logger)
            : base(dataProtectionProvider, options, logger)
        {
        }
    }

    public class RecuperoPasswordTokenProviderOptions : DataProtectionTokenProviderOptions
    {
        public RecuperoPasswordTokenProviderOptions()
        {
            Name = RecuperoPasswordTokenProvider<object>.Nome;
            TokenLifespan = RecuperoPasswordTokenProvider<object>.Durata;
        }
    }
}
