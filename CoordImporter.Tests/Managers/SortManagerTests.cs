using System.Numerics;
using CoordImporter.Managers;
using CoordImporter.Models;
using CoordImporter.Parsers;
using Dalamud.Plugin.Services;
using DitzyExtensions;
using DitzyExtensions.Collection;
using DitzyExtensions.Functional;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using XIVHuntUtils.Managers;
using XIVHuntUtils.Models;
using static CoordImporter.Managers.SortManager;
using static DitzyExtensions.EnumExtensions;

namespace CoordImporter.Tests.Managers;

[TestFixture]
public class SortManagerTests
{
    private static readonly Vector3 SpawnPoint = new Vector3(1, 2, 3);
    private static readonly Vector3 AetherytePosition = new Vector3(1, 8, 3);

    private static readonly IList<(Territory territory, uint id)> TerritoryIds = GetEnumValues<Territory>()
        .Select((territory, i) => (territory, (uint)i))
        .AsList();

    private static readonly IList<string> ExportedCoords =
        """
            0  Nuckelavee @ Lakeland ( 18.3 , 23.0 )
            1  Maliktender @ Amh Araeng ( 28.8 , 20.3 ) Instance TWO
            2  Grassman @ The Rak'tika Greatwood ( 16.7 , 24.1 )
            3  Minerva @ Garlemald ( 15.7 , 19.7 ) Instance ONE
            4  Yehehetoaua'pyo @ Shaaloani ( 31.3 , 22.9 )
            5  Cat's eye @ Living Memory ( 4.3 , 29.0 ) Instance TWO
            6  Minerva @ Garlemald ( 15.7 , 19.7 ) Instance TWO
            7  Maliktender @ Amh Araeng ( 28.8 , 20.3 ) Instance ONE
            8  Nariphon @ Lakeland ( 27.65, 15.35 )
            9  Rrax yity'a @ Yak T'el ( 24.8 , 33.0 )
            10 Cat's eye @ Living Memory ( 26.8 , 31.0 ) Instance ONE
            11 O poorest pauldia @ Il Mheg ( 10.8 , 20.4 )
            """.Split("\n").Select(line => line[3..]).AsList();

    private static readonly IList<Patch> PatchOrder =
    [
        Patch.EW,
        Patch.SHB,
        Patch.ARR,
        Patch.DT,
        Patch.HW,
        Patch.SB,
    ];

    private static uint SimpleSortTestCaseCounter = 1;
    private static readonly IList<TestCaseData> SimpleSortTestCases =
    [
        CreateSimpleSortTestCase(
            "IPM",
            0.SequenceTo(ExportedCoords.Count).AsList(),
            [3, 0, 8, 7, 11, 2, 9, 4, 10, 6, 1, 5],
            [SortCriteria.Instance, SortCriteria.Patch, SortCriteria.Map]
        ),
        CreateSimpleSortTestCase(
            "IM",
            0.SequenceTo(ExportedCoords.Count).AsList(),
            [0, 8, 3, 7, 9, 4, 11, 2, 10, 1, 6, 5],
            [SortCriteria.Instance, SortCriteria.Map]
        ),
        CreateSimpleSortTestCase(
            "PI",
            0.SequenceTo(ExportedCoords.Count).AsList(),
            [3, 6, 0, 2, 7, 8, 11, 1, 4, 9, 10, 5],
            [SortCriteria.Patch, SortCriteria.Instance]
        ),
        /*
         * if you check the marks for the expected coords, this case might look
         * weird. but note that Map sorting doesn't guarantee that instances of
         * a map will end up next to each other without Patch sorting enabled.
         * notably: garlemald, amh araeng, and yak t'el are all sort position 2
         * in their respective patches so all of them end up next to each other.
         */
        CreateSimpleSortTestCase(
            "MI",
            0.SequenceTo(ExportedCoords.Count).AsList(),
            [0, 8, 3, 7, 9, 1, 6, 4, 11, 2, 10, 5],
            [SortCriteria.Map, SortCriteria.Instance]
        ),
    ];

    private static uint AetheryteSortTestCaseCounter = 1;
    private static readonly IList<TestCaseData> AetheryteSortTestCases =
    [
        CreateAetheryteSortTestCase(
            "I :: Mixed Instances",
            [6, 10, 0],
            [0, 6, 10],
            [SortCriteria.Aetheryte],
            false, false, true
        ),
        CreateAetheryteSortTestCase(
            "I :: Map Instances",
            [6, 10, 0, 3],
            [0, 3, 10, 6],
            [SortCriteria.Aetheryte],
            false, false, true
        ),
        CreateAetheryteSortTestCase(
            "TI :: Map Instances",
            [6, 10, 0, 3],
            [0, 3, 6, 10],
            [SortCriteria.Aetheryte],
            false, true, true
        ),
        CreateAetheryteSortTestCase(
            "T :: Map Instances",
            [6, 10, 0, 3],
            [0, 6, 3, 10],
            [SortCriteria.Aetheryte],
            false, true, false
        ),
        CreateAetheryteSortTestCase(
            "I :: Map Duplicate",
            [6, 10, 0, 3, 8],
            [0, 3, 10, 8, 6],
            [SortCriteria.Aetheryte],
            false, false, true
        ),
        CreateAetheryteSortTestCase(
            "C :: Map Duplicate",
            [6, 10, 0, 3, 8],
            [0, 8, 6, 10, 3],
            [SortCriteria.Aetheryte],
            true, false, false
        ),
        CreateAetheryteSortTestCase(
            "CI :: Map Duplicate",
            [6, 10, 0, 3, 8],
            [0, 8, 3, 10, 6],
            [SortCriteria.Aetheryte],
            true, false, true
        ),
        CreateAetheryteSortTestCase(
            "CTI :: All Coords",
            0.SequenceTo(ExportedCoords.Count).AsList(),
            [0, 8, 7, 1, 11, 2, 3, 6, 9, 4, 10, 5],
            [SortCriteria.Aetheryte],
            true, true, true
        ),
        CreateAetheryteSortTestCase(
            "P+CTI :: All Coords",
            0.SequenceTo(ExportedCoords.Count).AsList(),
            [3, 6, 0, 8, 7, 1, 11, 2, 9, 4, 10, 5],
            [SortCriteria.Patch, SortCriteria.Aetheryte],
            true, true, true
        ),
    ];

    private ServiceProvider provider;

    private readonly IPluginLog logger = Substitute.For<IPluginLog>();
    private readonly IChatGui chat = Substitute.For<IChatGui>();
    private readonly IHuntManager huntManager = Substitute.For<IHuntManager>();
    private readonly ITravelManager travelManager = Substitute.For<ITravelManager>();
    private readonly IDataManagerManager dataManagerManager = Substitute.For<IDataManagerManager>();

    private CiConfiguration config;
    private SortManager sorter;

    [SetUp]
    public void SetUp()
    {
        config = new CiConfiguration().Initialize();
        config.PatchSortOrder = new List<Patch>(PatchOrder);

        provider = new ServiceCollection()
            .AddSingleton(logger)
            .AddSingleton(chat)
            .AddSingleton(huntManager)
            .AddSingleton(travelManager)
            .AddSingleton(dataManagerManager)
            .AddSingleton(config)
            .AddSingleton<ITrackerParser, BearParser>()
            .AddSingleton<ITrackerParser, FaloopParser>()
            .AddSingleton<ITrackerParser, SirenParser>()
            .AddSingleton<ITrackerParser, TurtleParser>()
            .AddSingleton<BearParser>()
            .AddSingleton<FaloopParser>()
            .AddSingleton<SirenParser>()
            .AddSingleton<TurtleParser>()
            .AddSingleton<Importer>()
            .AddSingleton<SortManager>()
            .BuildServiceProvider();

        sorter = provider.GetRequiredService<SortManager>();

        TerritoryIds.ForEach(t =>
        {
            dataManagerManager
                .GetMapDataByName(Arg.Is<string>(s => s.AsLower() == t.territory.Name()))
                .Returns(new MapData(t.id, t.id));

            huntManager
                .FindNearestSpawn2d(t.id, Arg.Any<Vector2>())
                .Returns(SpawnPoint);

            // Set up nodes so that later patches are farther from the aetheryte,
            // but the second find for a given territory is much farther away.
            travelManager
                .FindNearestTravelNode3d(t.id, Arg.Any<Vector3>())
                .Returns(
                    CreateTravelNode(t.territory, t.id),
                    CreateTravelNode(t.territory, t.id + 1000)
                );
        });
    }

    [TearDown]
    public void TearDown()
    {
        provider.Dispose();
    }

    [TestCaseSource(nameof(SimpleSortTestCases))]
    public void TestSortWithoutAetheryte(
        IList<int> inputCoords,
        IList<int> expectedCoords,
        IList<SortCriteria> sortOrder
    )
    {
        TestSorting(inputCoords, expectedCoords, sortOrder);
        chat.DidNotReceiveWithAnyArgs().PrintError("");
    }

    [TestCaseSource(nameof(AetheryteSortTestCases))]
    public void TestSortWithAetheryte(
        IList<int> inputCoords,
        IList<int> expectedCoords,
        IList<SortCriteria> sortOrder,
        bool completeMaps,
        bool mapsTogether,
        bool sortInstances
    )
    {
        config.AetheryteCompleteMaps = completeMaps;
        config.AetheryteKeepMapsTogether = mapsTogether;
        config.AetheryteSortInstances = sortInstances;
        TestSorting(
            inputCoords,
            expectedCoords,
            sortOrder
        );
        chat.DidNotReceiveWithAnyArgs().PrintError("");
    }

    private void TestSorting(IList<int> inputCoords, IList<int> expectedCoords, IList<SortCriteria> sortOrder)
    {
        var textInput = inputCoords.Select(i => ExportedCoords[i]).Join("\n");
        var expected = expectedCoords.Select(i => ExportedCoords[i]).Join("\n");

        config.ActiveSortOrder = new List<SortCriteria>(sortOrder);

        var actual = sorter.SortEntries(textInput);

        Assert.That(actual, Is.EqualTo(expected).NoClip);
    }

    private static TestCaseData CreateSimpleSortTestCase(
        string caseName,
        IList<int> inputCoords,
        IList<int> expectedCoords,
        IList<SortCriteria> sortOrder
    ) => new TestCaseData(inputCoords, expectedCoords, sortOrder)
        .SetName($"Simple Sort Case {SimpleSortTestCaseCounter++} // {caseName}");

    private static TestCaseData CreateAetheryteSortTestCase(
        string caseName,
        IList<int> inputCoords,
        IList<int> expectedCoords,
        IList<SortCriteria> sortOrder,
        bool completeMaps,
        bool mapsTogether,
        bool sortInstances
    ) => new TestCaseData(
            inputCoords,
            expectedCoords,
            sortOrder,
            completeMaps,
            mapsTogether,
            sortInstances
        )
        .SetName($"Aetheryte Sort Case {AetheryteSortTestCaseCounter++} // {caseName}");

    private static TravelNode CreateTravelNode(Territory territory, float distanceModifier)
    {
        return new TravelNode(
            new Aetheryte("test aetheryte", territory, AetherytePosition, true),
            true,
            territory,
            distanceModifier,
            AetherytePosition,
            ""
        );
    }
}
