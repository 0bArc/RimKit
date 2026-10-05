using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RimWorld;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.time: calendar and speed. Ops use the singular domain "time".
    internal static class ApiTime
    {
        public static void Register()
        {
            ApiRegistry.Register("time.now", Now, "gameplay", "0.5.0");
            ApiRegistry.Register("time.speed", Speed, "gameplay", "0.5.0");
            ApiRegistry.Register("time.set_speed", SetSpeed, "gameplay", "0.5.0");
            ApiRegistry.Register("time.paused", Paused, "gameplay", "0.5.0");
            ApiRegistry.Register("time.set_paused", SetPaused, "gameplay", "0.5.0");
            ApiRegistry.Register("time.date_text", DateText, "gameplay", "0.5.0");
        }

        private static Map CurrentMap() => Find.CurrentMap ?? Find.AnyPlayerHomeMap;

        private static string Num(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

        // Calendar fields for the current map's location. Falls back to world time when no map is loaded.
        private static string Now(Dictionary<string, string> args)
        {
            if (Current.Game == null || Find.TickManager == null)
            {
                return Fail("RK3001", "no game is loaded");
            }

            Map map = CurrentMap();
            var sb = new StringBuilder("{");
            sb.Append("\"ticks\":").Append(Find.TickManager.TicksGame.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"abs_ticks\":").Append(Find.TickManager.TicksAbs.ToString(CultureInfo.InvariantCulture));
            if (map != null)
            {
                sb.Append(",\"hour\":").Append(GenLocalDate.HourOfDay(map).ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"hour_float\":").Append(Num(GenLocalDate.HourFloat(map)));
                sb.Append(",\"day_of_year\":").Append(GenLocalDate.DayOfYear(map).ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"day_of_season\":").Append(GenLocalDate.DayOfSeason(map).ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"day_of_quadrum\":").Append(GenLocalDate.DayOfQuadrum(map).ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"year\":").Append(GenLocalDate.Year(map).ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"season\":").Append(JsonLite.Quote(GenLocalDate.Season(map).ToString()));
                sb.Append(",\"quadrum\":").Append(JsonLite.Quote(GenDate.Quadrum(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile).x).ToString()));
                sb.Append(",\"day_percent\":").Append(Num(GenLocalDate.DayPercent(map)));
                sb.Append(",\"is_night\":").Append(GenLocalDate.HourOfDay(map) < 6 || GenLocalDate.HourOfDay(map) >= 22 ? "true" : "false");
            }

            sb.Append(",\"ticks_per_hour\":").Append(GenDate.TicksPerHour.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"ticks_per_day\":").Append(GenDate.TicksPerDay.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            return OkJson(sb.ToString());
        }

        private static string Speed(Dictionary<string, string> args)
        {
            TickManager tm = Find.TickManager;
            if (tm == null) return Fail("RK3001", "no game is loaded");
            return OkStr(tm.CurTimeSpeed.ToString());
        }

        // speed: Paused, Normal, Fast, Superfast, Ultrafast (case insensitive) or 0 to 4.
        private static string SetSpeed(Dictionary<string, string> args)
        {
            TickManager tm = Find.TickManager;
            if (tm == null) return Fail("RK3001", "no game is loaded");
            string raw = Str(args, "speed");
            TimeSpeed speed;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            {
                if (n < 0 || n > 4) return Fail("RK1001", "speed must be 0 to 4");
                speed = (TimeSpeed)n;
            }
            else if (!System.Enum.TryParse(raw, true, out speed) || !System.Enum.IsDefined(typeof(TimeSpeed), speed))
            {
                return Fail("RK1001", "speed must be Paused, Normal, Fast, Superfast, Ultrafast or 0 to 4");
            }

            tm.CurTimeSpeed = speed;
            return OkStr(speed.ToString());
        }

        private static string Paused(Dictionary<string, string> args)
        {
            TickManager tm = Find.TickManager;
            if (tm == null) return Fail("RK3001", "no game is loaded");
            return OkBool(tm.Paused);
        }

        private static string SetPaused(Dictionary<string, string> args)
        {
            TickManager tm = Find.TickManager;
            if (tm == null) return Fail("RK3001", "no game is loaded");
            bool want = Bool(args, "paused");
            if (want != tm.Paused)
            {
                tm.TogglePaused();
            }

            return OkBool(tm.Paused);
        }

        // Readable date for an absolute tick at the current map's location, for example "5th of Aprimay, 5500".
        private static string DateText(Dictionary<string, string> args)
        {
            Map map = CurrentMap();
            if (map == null || Find.TickManager == null) return Fail("RK3001", "no map is loaded");
            int ticks = Int(args, "ticks");
            long abs = string.IsNullOrEmpty(Str(args, "ticks")) ? Find.TickManager.TicksAbs : Find.TickManager.gameStartAbsTick + ticks;
            var longLat = Find.WorldGrid.LongLatOf(map.Tile);
            return OkStr(GenDate.DateFullStringAt(abs, longLat));
        }
    }
}
