using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text;
using Dalamud.Plugin;

namespace CoordImporter;

public static class Utils
{
    #region extentions

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

    public static string DataFilePath(this DalamudPluginInterface pluginInterface, string dataFilename) => Path.Combine(
        pluginInterface.AssemblyLocation.Directory?.FullName!,
        "Data",
        dataFilename
    );

    #endregion
}
