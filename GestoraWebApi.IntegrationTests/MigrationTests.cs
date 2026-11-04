using GestoraWebApi.Context;
using GestoraWebApi.IntegrationTests.Infrastruttura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestoraWebApi.IntegrationTests
{
    /// <summary>Le migration ripetute tutte su un database vuoto, come per un'installazione nuova.</summary>
    public class MigrationTests
    {
        private const string DatabaseVuoto = "gestora_test_migration";

        [Fact]
        public async Task DaZero_SiApplicanoTutte_ENonRestaNessunaModificaFuori()
        {
            var stringa = new NpgsqlConnectionStringBuilder(DatabaseDiProva.StringaDiConnessione());
            var amministrazione = new NpgsqlConnectionStringBuilder(stringa.ConnectionString) { Database = "postgres" };
            var vuoto = new NpgsqlConnectionStringBuilder(stringa.ConnectionString) { Database = DatabaseVuoto, Pooling = false };

            await EseguiAsync(amministrazione.ConnectionString, $"DROP DATABASE IF EXISTS {DatabaseVuoto}");
            await EseguiAsync(amministrazione.ConnectionString, $"CREATE DATABASE {DatabaseVuoto}");

            try
            {
                var opzioni = new DbContextOptionsBuilder<GestoraContext>().UseNpgsql(vuoto.ConnectionString).Options;
                await using var contesto = new GestoraContext(opzioni);

                await contesto.Database.MigrateAsync();

                Assert.Empty(await contesto.Database.GetPendingMigrationsAsync());
                Assert.False(contesto.Database.HasPendingModelChanges());
            }
            finally
            {
                await EseguiAsync(amministrazione.ConnectionString, $"DROP DATABASE IF EXISTS {DatabaseVuoto} WITH (FORCE)");
            }
        }

        private static async Task EseguiAsync(string stringaDiConnessione, string comando)
        {
            await using var connessione = new NpgsqlConnection(stringaDiConnessione);
            await connessione.OpenAsync();
            await using var sql = new NpgsqlCommand(comando, connessione);
            await sql.ExecuteNonQueryAsync();
        }
    }
}
