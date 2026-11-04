using GestoraWebApi.Common;

namespace GestoraWebApi.IntegrationTests.Infrastruttura
{
    /// <summary>
    /// L'ora vera più uno spostamento. I token restano validi (il controllo del JWT usa l'ora del
    /// sistema) e un test può portarsi avanti nel tempo: per esempio a dopo la fine di una fascia.
    /// </summary>
    public sealed class OrologioDiProva : IClock
    {
        public TimeSpan Spostamento { get; set; } = TimeSpan.Zero;

        public DateTime UtcNow => DateTime.UtcNow + Spostamento;

        public DateTime NowInRome => TimeZoneInfo.ConvertTime(UtcNow, SystemClock.RomeTimeZone);

        public DateOnly TodayInRome => DateOnly.FromDateTime(NowInRome);

        /// <summary>Porta l'orologio al giorno e all'ora indicati, ora di Roma.</summary>
        public void VaiA(DateOnly giorno, TimeOnly ora)
        {
            var locale = giorno.ToDateTime(ora, DateTimeKind.Unspecified);
            Spostamento = TimeZoneInfo.ConvertTimeToUtc(locale, SystemClock.RomeTimeZone) - DateTime.UtcNow;
        }
    }
}
