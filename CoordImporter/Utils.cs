using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text;

namespace CoordImporter;

public static class Utils
{
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        var values = source.ToList();
        foreach (var value in values)
        {
            action.Invoke(value);
        }

        return values;
    }

    public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> source) =>
        source.SelectMany(values => values);

    public static Maybe<T> AsMaybe<T>(this T? value, Action emptyAction)
    {
        Maybe<T> maybeVal = Maybe.From(value)!;
        maybeVal.ExecuteNoValue(emptyAction);
        return maybeVal;
    }

    public static string AsInstanceIcon(this uint? instance) =>
        instance is >= 1 and <= 9
            ? (SeIconChar.Instance1 + (int)instance! - 1).ToIconString()
            : string.Empty;
}
