using CoordImporter.Managers;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using System.Collections.Immutable;
using System.Numerics;
using CoordImporter.Models;
using CoordImporter.Parsers;
using DitzyExtensions.Collection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoordImporter.Tests
{
    [TestFixture]
    public class ParserTests
    {
        private static readonly string LinkChar = SeIconChar.LinkMarker.ToIconString();
        private static readonly string I2Char = SeIconChar.Instance2.ToIconString();

        private static readonly IList<ParserType> AllParserTypes = 
            (Enum.GetValuesAsUnderlyingType<ParserType>() as ParserType[])!
            .AsList();

        private static readonly IList<object[]> NonInstanceTestCases = OrganizeTestCases(
            CreateTestCase(ParserType.Siren,
                           // trailing space is actually in the Siren output >:T
                           $@"(Maybe: Stolas) {LinkChar}The Dravanian Hinterlands ( 26.85  , 20.05 ) ",
                           "Stolas", "The Dravanian Hinterlands", new Vector2(26.85f, 20.05f)
            ),
            CreateTestCase(ParserType.Faloop,
                           @"Raiden [S]: Gamma - Yanxia ( 23.6, 11.4 )",
                           "Gamma", "Yanxia", new Vector2(23.6f, 11.4f)
            ),
            CreateTestCase(ParserType.Bear,
                           @"The Rak'tika Greatwood ( 14.6 , 22.3 ) Supay",
                           "Supay", "The Rak'tika Greatwood", new Vector2(14.6f, 22.3f)
            ),
            CreateTestCase(ParserType.Turtle,
                           $@"Sugriva @ {LinkChar}Thavnair ( 18.65 , 11.55 ) ",
                           "Sugriva", "Thavnair", new Vector2(18.65f, 11.55f)
            )
        );

        private static readonly IList<object[]> InstanceTestCases = OrganizeTestCases(
            CreateTestCase(ParserType.Siren,
                           // trailing space is actually in the Siren output >:T
                           $@"(Yilan) {LinkChar}Thavnair{I2Char} ( 26.8  , 20.9 )  (Instance TWO) ",
                           "Yilan", "Thavnair", new Vector2(26.8f, 20.9f), 2
            ),
            CreateTestCase(ParserType.Faloop,
                           @"Odin [S]: Vogaal Ja - Middle La Noscea (1) ( 8.2, 32.65 )",
                           "Vogaal Ja", "Middle La Noscea", new Vector2(8.2f, 32.65f), 1
            ),
            CreateTestCase(ParserType.Bear,
                           @"Thavnair 3 ( 27.6 , 25.6 ) Sugriva",
                           "Sugriva", "Thavnair", new Vector2(27.6f, 25.6f), 3
            ),
            CreateTestCase(ParserType.Turtle,
                           $@"The raintriller @ {LinkChar}Kozama'uka{I2Char} ( 20.30 , 28.40 ) Instance TWO",
                           "The raintriller", "Kozama'uka", new Vector2(20.30f, 28.40f), 2
            )
        );

        private static IList<object[]> AllTestCases =
            NonInstanceTestCases.Concat(InstanceTestCases).AsList();

        private IPluginLog _Logger = Substitute.For<IPluginLog>();
        private IChatGui _Chat = Substitute.For<IChatGui>();
        private IDataManager _DataManager = Substitute.For<IDataManager>();
        private IDictionary<ParserType, ITrackerParser> Parsers { get; set; }
        private IDataManagerManager _DataManagerManager { get; set; }

        [SetUp]
        public void Setup()
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            _DataManagerManager = Substitute.For<IDataManagerManager>();
            builder.Services.AddSingleton(_Logger);
            builder.Services.AddSingleton(_Chat);
            builder.Services.AddSingleton(_DataManager);
            builder.Services.AddSingleton(_DataManagerManager);
            builder.Services.AddSingleton<ITrackerParser, BearParser>();
            builder.Services.AddSingleton<ITrackerParser, FaloopParser>();
            builder.Services.AddSingleton<ITrackerParser, SirenParser>();
            builder.Services.AddSingleton<ITrackerParser, TurtleParser>();
            builder.Services.AddSingleton<BearParser>();
            builder.Services.AddSingleton<FaloopParser>();
            builder.Services.AddSingleton<SirenParser>();
            builder.Services.AddSingleton<TurtleParser>();
            builder.Services.AddSingleton<Importer>();
            using IHost host = builder.Build();

            Parsers = new Dictionary<ParserType, ITrackerParser>()
            {
                { ParserType.Bear, host.Services.GetService<BearParser>()! },
                { ParserType.Faloop, host.Services.GetService<FaloopParser>()! },
                { ParserType.Siren, host.Services.GetService<SirenParser>()! },
                { ParserType.Turtle, host.Services.GetService<TurtleParser>()! },

            }.VerifyEnumDictionary();
        }

        [TestCaseSource(nameof(AllTestCases))]
        public void TestCanParse(ParserType parserType, ParserTestCase testCase)
        {
            // DATA
            var parser = Parsers[parserType];
            var expected = Parsers
                           .Select(parserEntry =>
                                       parserEntry.Key == parserType ? parserEntry.Key : (ParserType?)null
                           )
                           .AsList();

            // WHEN
            var actual = Parsers
                         .Select(parserEntry =>
                                     parserEntry.Value.CanParseLine(testCase.InputLine) ? parserEntry.Key : (ParserType?)null
                             )
                         .AsList();

            // THEN
            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [TestCaseSource(nameof(AllTestCases))]
        public void TestParsesSuccessfully(ParserType parserType, ParserTestCase testCase)
        {
            // DATA
            var parser = Parsers[parserType];
            var territoryId = (uint)Random.Shared.Next();
            var mapId = (uint)Random.Shared.Next();
            var expected = testCase.ToMarkData(territoryId, mapId);

            // GIVEN
            _DataManagerManager.GetMapDataByName(Arg.Any<string>())
                               .Returns(new MapData(territoryId, mapId));

            // WHEN
            var actual = parser.Parse(testCase.InputLine);

            // THEN
            _DataManagerManager.Received(Quantity.Exactly(1)).GetMapDataByName(expected.MapName);

            Assert.That(actual, Is.EqualTo(Result.Success<MarkData, string>(expected)));
        }

        private static IList<object[]> OrganizeTestCases(
            params (ParserType parserType, ParserTestCase testCase)[] cases
        ) =>
            cases
                .AsDict()
                .VerifyEnumDictionary()
                .AsPairs()
                .Select(testCase => new object[] {testCase.key, testCase.value})
                .AsList();

        private static (ParserType, ParserTestCase) CreateTestCase(
            ParserType parserType,
            string inputLine,
            string markName,
            string mapName,
            Vector2 position,
            uint? instance = null
        ) =>
            (parserType, new ParserTestCase(inputLine, markName, mapName, position, instance));

        public enum ParserType
        {
            Siren,
            Faloop,
            Bear,
            Turtle,
        }
    }

    public record struct ParserTestCase(
        string InputLine,
        string MarkName,
        string MapName,
        Vector2 Position,
        uint? Instance = null)
    {
        public MarkData ToMarkData(uint territoryId, uint mapId) =>
            new(MarkName, MapName, territoryId, mapId, Instance, Position);
    }
}
