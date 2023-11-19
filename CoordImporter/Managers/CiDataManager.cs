using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CoordImporter.Managers;

using CorrectionDict = IReadOnlyDictionary<string, string>;

public class CiDataManager : ICiDataManager
{
    private readonly CorrectionDict corrections;
    
    public CiDataManager(string customNamesFilename)
    {
        corrections = LoadCorrections(customNamesFilename);
    }
    
    public string CorrectMarkName(string markName)
    {
        return corrections.TryGetValue(markName.ToLowerInvariant(), out var correction) ? correction : markName;
    }

    public string CorrectMapName(string mapName)
    {
        return corrections.TryGetValue(mapName.ToLowerInvariant(), out var correction) ? correction : mapName;
    }

    private static CorrectionDict LoadCorrections(string customNamesFilename)
    {
        return JsonConvert.DeserializeObject<IDictionary<string, JObject>>(File.ReadAllText(customNamesFilename))!
            .SelectMany(trackerCustomizations =>
            {
                var tracker = trackerCustomizations.Key!;
                var customizations = trackerCustomizations.Value! as IDictionary<string, JToken?>;
                return customizations
                    .Select(correction =>
                    {
                        var nameFromTracker = correction.Key!.ToLowerInvariant();
                        var correctName = correction.Value!.Value<string>()!;
                        return (nameFromTracker, correctName);
                    });
            })
            .ToImmutableDictionary(
                correction => correction.nameFromTracker,
                correction => correction.correctName
            );
    }
}
