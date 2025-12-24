using CSharpFunctionalExtensions;
using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using CoordImporter.Managers;
using CoordImporter.Models;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using XIVHuntUtils.Models;

using SeStringPayloads = System.Collections.Generic.List<Dalamud.Game.Text.SeStringHandling.Payload>;

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
        var maybeVal = Maybe.From(value)!;
        maybeVal.ExecuteNoValue(emptyAction);
        return maybeVal;
    }

    public static string AsInstanceIcon(this uint? instance) =>
        instance is >= 1 and <= 9
            ? (SeIconChar.Instance1 + (int)instance! - 1).ToIconString()
            : string.Empty;
    
    public static SeStringPayloads CreateAetheryteMapLink(this IDataManagerManager dmm, Aetheryte aetheryte, uint? instance)
    {
        var aetheryteNamePayload = new TextPayload($"【 {aetheryte.Name} 】");
        
        var payloads = new SeStringPayloads();
        dmm
            .GetMapDataByName(aetheryte.Territory.Name())
            .Match(
                Result.Success<MapData, TextPayload>,
                () => new TextPayload("(unclickable)")
            )
            .Map(map =>
                new MapLinkPayload(
                    aetheryte.Territory.Id(),
                    map.RowId,
                    aetheryte.Position.X,
                    aetheryte.Position.Y
                )
            )
            .Match(
            payload =>
            {
                payloads.Add(payload);
                payloads.AddRange(SeString.TextArrowPayloads);
                payloads.AddRange(
                    payload,
                    new TextPayload($"{payload.PlaceName}{instance.AsInstanceIcon()}"),
                    aetheryteNamePayload,
                    RawPayload.LinkTerminator
                );
            },
            errorPayload => payloads.AddRange(errorPayload, aetheryteNamePayload)
        );

        return payloads;
    }

    // This is a custom version of Dalamud's CreateMapLink method. It includes the mark name and the instance ID
    public static SeStringPayloads CreateMapLink(MarkData markData)
    {
        var mapLinkPayload =
            new MapLinkPayload(markData.TerritoryId, markData.MapId, markData.Position.X, markData.Position.Y);
        var text = mapLinkPayload.PlaceName + markData.Instance.AsInstanceIcon() + " " +
                   mapLinkPayload.CoordinateString;

        var payloads = new SeStringPayloads();
        payloads.Add(mapLinkPayload);
        payloads.AddRange(SeString.TextArrowPayloads);
        payloads.AddRange([
            new TextPayload(text),
            new TextPayload($"【 {markData.MarkName} 】"),
            RawPayload.LinkTerminator
        ]);
        
        return payloads;
    }
}
