using System.Globalization;

namespace KukaManager.Utils;

public static class Value
{
    public static string S(IDictionary<string, object?> row, string key, string fallback = "")
        => row.TryGetValue(key, out var v) && v is not null && v is not DBNull ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback : fallback;

    public static long L(IDictionary<string, object?> row, string key, long fallback = 0)
        => row.TryGetValue(key, out var v) && v is not null && v is not DBNull ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : fallback;

    public static int I(IDictionary<string, object?> row, string key, int fallback = 0)
        => checked((int)L(row, key, fallback));

    public static double D(IDictionary<string, object?> row, string key, double fallback = 0)
        => row.TryGetValue(key, out var v) && v is not null && v is not DBNull ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : fallback;

    public static bool B(IDictionary<string, object?> row, string key, bool fallback = false)
        => row.TryGetValue(key, out var v) && v is not null && v is not DBNull ? Convert.ToInt64(v, CultureInfo.InvariantCulture) != 0 : fallback;

    public static long Cents(object? value)
    {
        if (value is null) return 0;
        if (!decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            throw new InvalidOperationException("金额必须是数字");
        if (d < 0) throw new InvalidOperationException("金额必须是非负数字");
        return decimal.ToInt64(decimal.Round(d * 100m, 0, MidpointRounding.AwayFromZero));
    }

    public static string Money(long fen) => (fen / 100m).ToString("N2", CultureInfo.GetCultureInfo("zh-CN"));
    public static string NewId() => Guid.NewGuid().ToString("N");
}
