using GestoraWebApi.Auth;
using GestoraWebApi.Context;
using GestoraWebApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace GestoraWebApi.Extensions
{
    public static class AuthenticationExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            //Configuration Identity
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // SEC-007 (V2-029): le stesse regole di Validators/RegolePassword (8 caratteri,
                // maiuscola, numero, carattere speciale). Il validator le controlla per primo e
                // dà i messaggi; queste sono la seconda rete, se un giorno un ingresso lo saltasse.
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireDigit = true;

                // Il login cerca l'utente per email (FindByEmailAsync): con due account sulla
                // stessa email solleva un'eccezione e quel login darebbe 500 per sempre. Il
                // default di Identity e' false, e chiunque puo' registrarsi.
                options.User.RequireUniqueEmail = true;

                // Il nome utente e' quello mostrato (il login e' per email): deve poter essere
                // "Fabio Iacurto" o "D'Angelo". Il default di Identity non ammette ne' spazi ne'
                // apostrofi ne' lettere accentate. Spazi in testa/coda e doppi: RegisterDTOValidator.
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ 'àèéìòùÀÈÉÌÒÙ";

                // V2-027: il blocco di Identity (per account) NON si usa più. Bloccava l'account a
                // chiunque sbagliasse 5 volte, quindi chiunque poteva chiudere fuori l'Admin. Le
                // password sbagliate le conta Infrastructure/Auth/TentativiAccesso, per email +
                // indirizzo, e il login usa CheckPasswordAsync. Non riattivarlo pensando che manchi.
                options.Lockout.AllowedForNewUsers = false;

                // V2-019: il link per reimpostare la password scade in 2 ore, non in 24.
                options.Tokens.PasswordResetTokenProvider = RecuperoPasswordTokenProvider<ApplicationUser>.Nome;
            })
            .AddEntityFrameworkStores<GestoraContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<RecuperoPasswordTokenProvider<ApplicationUser>>(RecuperoPasswordTokenProvider<ApplicationUser>.Nome)
            .AddErrorDescriber<IdentityErrorDescriberItaliano>();

            //Configuration JWT            
            var jwtSettings = configuration.GetSection("JwtSettings");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),

                    // FASE 10: senza questo, .NET applica una tolleranza di 5 minuti oltre la
                    // scadenza dichiarata (il default della libreria, pensato per orologi di
                    // server leggermente disallineati). Un token con ExpiryMinutes=60 restava
                    // quindi valido fino a 65 minuti. Qui l'orologio e' lo stesso server che lo
                    // ha emesso, non serve tolleranza: la scadenza deve essere quella vera.
                    ClockSkew = TimeSpan.Zero
                };

                // Personalizzazione risposta 401
                options.Events = new JwtBearerEvents
                {
                    // V2-028: il token arriva nel cookie HttpOnly. Se la richiesta porta l'header
                    // Authorization (Swagger, curl) si lascia fare al middleware, che legge quello.
                    OnMessageReceived = context =>
                    {
                        context.Token = CookieSessione.TokenDallaRichiesta(context.Request);
                        return Task.CompletedTask;
                    },

                    // AUD-M6: senza questo controllo un token restava valido fino alla scadenza
                    // anche dopo che all'utente era stato tolto un ruolo o l'account era stato
                    // eliminato. Costa una lettura dell'utente per richiesta autenticata.
                    OnTokenValidated = async context =>
                    {
                        var userManager = context.HttpContext.RequestServices
                            .GetRequiredService<UserManager<ApplicationUser>>();

                        // Stessa lettura di ClaimsPrincipalExtensions.GetAuthenticatedUserId.
                        var userId = context.Principal?.FindFirstValue("sub")
                                     ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var stamp = context.Principal?.FindFirstValue(JwtTokenGenerator.ClaimSecurityStamp);
                        var user = userId == null ? null : await userManager.FindByIdAsync(userId);

                        if (user == null || stamp == null || stamp != await userManager.GetSecurityStampAsync(user))
                            context.Fail("Sessione non piu' valida: ruoli o account modificati.");
                    },

                    OnChallenge = context =>
                    {
                        // Impedisce la risposta di default (WWW-Authenticate senza body)
                        context.HandleResponse();

                        // V2-028: un cookie con un token scaduto o non più valido non serve a
                        // nulla; si toglie, così il browser smette di mandarlo.
                        if (context.Request.Cookies.ContainsKey(CookieSessione.Nome))
                            CookieSessione.Cancella(context.Response);

                        //Imposta lo status e il content-type
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        //Scrive il body JSON
                        var result = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            message = "Sessione assente o scaduta: effettua di nuovo l'accesso."
                        });

                        return context.Response.WriteAsync(result);
                    }
                };
            });

            return services;
        }
    }
}
