using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GestoraWebApi.IntegrationTests.Infrastruttura
{
    /// <summary>
    /// Il database su cui girano i test: sempre <c>gestora_test</c>, sempre su questa macchina.
    /// Fra un test e l'altro le tabelle si svuotano, quindi i due controlli non sono formalità:
    /// una stringa di connessione sbagliata non deve mai arrivare a Neon.
    /// </summary>
    internal static class DatabaseDiProva
    {
        private const string NomeDatabase = "gestora_test";

        /// <summary>In CI la stringa arriva da qui; in locale si riusa quella degli user secrets dell'API.</summary>
        private const string VariabileAmbiente = "GESTORA_TEST_DB";

        private const string UserSecretsApi = "aa9f6e84-1217-48e1-9783-b07f152f7874";

        private static readonly string[] HostAmmessi = ["localhost", "127.0.0.1", "::1"];

        public static string StringaDiConnessione()
        {
            var sorgente = Environment.GetEnvironmentVariable(VariabileAmbiente);
            if (string.IsNullOrWhiteSpace(sorgente))
                sorgente = new ConfigurationBuilder().AddUserSecrets(UserSecretsApi).Build()
                    .GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(sorgente))
                throw new InvalidOperationException(
                    $"Stringa di connessione mancante: impostare {VariabileAmbiente} oppure gli user secrets dell'API.");

            var stringa = new NpgsqlConnectionStringBuilder(sorgente) { Database = NomeDatabase };

            if (!HostAmmessi.Contains(stringa.Host, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "I test d'integrazione svuotano il database: partono solo su un PostgreSQL di questa macchina.");

            return stringa.ConnectionString;
        }

        public static async Task CreaSeMancaAsync(string stringaDiConnessione)
        {
            var stringa = new NpgsqlConnectionStringBuilder(stringaDiConnessione) { Database = "postgres" };

            await using var connessione = new NpgsqlConnection(stringa.ConnectionString);
            await connessione.OpenAsync();

            await using var esiste = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @nome", connessione);
            esiste.Parameters.AddWithValue("nome", NomeDatabase);

            if (await esiste.ExecuteScalarAsync() is not null)
                return;

            await using var crea = new NpgsqlCommand($"CREATE DATABASE {NomeDatabase}", connessione);
            await crea.ExecuteNonQueryAsync();
        }
    }
}
