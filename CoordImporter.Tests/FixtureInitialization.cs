using DitzyExtensions.Collection;
using XIVHuntUtils.Models;
using static DitzyExtensions.EnumExtensions;

namespace CoordImporter.Tests;

[SetUpFixture]
public class FixtureInitialization
{
    [OneTimeSetUp]
    public void InitializeFixtures()
    {
        var territoryIds =
            GetEnumValues<Territory>()
                .Select((territory, i) => (territory, (uint)i))
                .AsList();

        TerritoryExtensions.SetTerritoryInstances(new Dictionary<uint, uint>(), territoryIds);
    }
}
