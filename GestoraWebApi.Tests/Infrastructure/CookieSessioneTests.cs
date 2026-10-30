using GestoraWebApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GestoraWebApi.Tests.Infrastructure;

/// <summary>
/// V2-028 — il token sta in un cookie che JavaScript non può leggere. Si guarda l'header
/// Set-Cookie che arriva al browser, perché sono i suoi attributi a fare la sicurezza.
/// </summary>
public class CookieSessioneTests
{
    private static readonly DateTime Adesso = new(2026, 10, 30, 18, 0, 0, DateTimeKind.Utc);

    private static string Token() => new JwtTokenGenerator(
        Options.Create(new JwtSettings
        {
            Secret = "segreto-di-prova-lungo-almeno-32-caratteri",
            Issuer = "GestoraWebApi",
            Audience = "GestoraClient",
            ExpiryMinutes = 60,
            MaxSessionHours = 12
        }),
        new TestClock(Adesso)).GenerateToken("utente-1", "staff@gestora.it", ["Staff"], "stamp-1");

    private static string SetCookie(HttpContext context) => context.Response.Headers.SetCookie.ToString();

    private static HttpRequest RichiestaConCookie(string? authorization = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{CookieSessione.Nome}=token-dal-cookie";
        if (authorization != null)
            context.Request.Headers.Authorization = authorization;
        return context.Request;
    }

    [Fact]
    public void Scrivi_CookieHttpOnlySecureStrict_SuTuttoIlSito()
    {
        var context = new DefaultHttpContext();

        CookieSessione.Scrivi(context.Response, Token());

        var cookie = SetCookie(context);
        Assert.StartsWith($"{CookieSessione.Nome}=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        // Il prefisso __Host- non ammette un dominio: il browser rifiuterebbe il cookie.
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scrivi_RestituisceLaScadenzaDelToken_EIlCookieScadeInsieme()
    {
        var context = new DefaultHttpContext();

        var scadenza = CookieSessione.Scrivi(context.Response, Token());

        Assert.Equal(Adesso.AddMinutes(60), scadenza);
        Assert.Contains("expires=Fri, 30 Oct 2026 19:00:00 GMT", SetCookie(context), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cancella_CookieGiaScaduto_ConGliStessiAttributi()
    {
        var context = new DefaultHttpContext();

        CookieSessione.Cancella(context.Response);

        var cookie = SetCookie(context);
        Assert.StartsWith($"{CookieSessione.Nome}=;", cookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scrivi_AncheIlCookieCsrf_LeggibileDaJavaScript_ConLaStessaScadenza()
    {
        var context = new DefaultHttpContext();

        CookieSessione.Scrivi(context.Response, Token());

        var csrf = context.Response.Headers.SetCookie.Single(c => c!.StartsWith($"{CookieCsrf.Nome}="))!;
        Assert.Matches($"^{CookieCsrf.Nome}=[0-9A-F]{{64}};", csrf);
        Assert.DoesNotContain("httponly", csrf, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", csrf, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", csrf, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=Fri, 30 Oct 2026 19:00:00 GMT", csrf, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scrivi_OgniVoltaUnValoreCsrfNuovo()
    {
        string ValoreCsrf()
        {
            var context = new DefaultHttpContext();
            CookieSessione.Scrivi(context.Response, Token());
            return context.Response.Headers.SetCookie.Single(c => c!.StartsWith($"{CookieCsrf.Nome}="))!.Split(';')[0];
        }

        Assert.NotEqual(ValoreCsrf(), ValoreCsrf());
    }

    [Fact]
    public void Cancella_ToglieAncheIlCookieCsrf()
    {
        var context = new DefaultHttpContext();

        CookieSessione.Cancella(context.Response);

        Assert.Contains($"{CookieCsrf.Nome}=;", SetCookie(context));
    }

    [Fact]
    public void TokenDallaRichiesta_SenzaAuthorization_PrendeIlCookie()
        => Assert.Equal("token-dal-cookie", CookieSessione.TokenDallaRichiesta(RichiestaConCookie()));

    [Fact]
    public void TokenDallaRichiesta_ConAuthorization_LasciaFareAlMiddleware()
        => Assert.Null(CookieSessione.TokenDallaRichiesta(RichiestaConCookie("Bearer token-dall-header")));

    [Fact]
    public void TokenDallaRichiesta_SenzaCookie_Null()
        => Assert.Null(CookieSessione.TokenDallaRichiesta(new DefaultHttpContext().Request));
}
