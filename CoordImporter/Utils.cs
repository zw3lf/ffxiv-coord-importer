using Dalamud.Game.Text;
using System;
using System.Collections.Generic;

namespace CoordImporter;

public static class Utils
{
    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var value in source)
        {
            action.Invoke(value);
        }
    }

    public static string AsInstanceIcon(this uint? instance) =>
        instance is >= 1 and <= 9
            ? (SeIconChar.Instance1 + (int)instance! - 1).ToIconString()
            : string.Empty;
}
