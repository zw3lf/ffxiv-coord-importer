using CoordImporter.Managers;
using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using System.Collections.Immutable;
using System.Numerics;

namespace CoordImporter.Tests
{
    [TestFixture]
    public class ParserTests
    {
        private static readonly string LinkChar = SeIconChar.LinkMarker.ToIconString();
        private static readonly string I2Char = SeIconChar.Instance2.ToIconString();
        
        // trailing space is actually in the Siren output >:T
        private static readonly string TestSirenInputLine = $@"(Maybe: Stolas) {LinkChar}The Dravanian Hinterlands ( 26.85  , 20.05 ) ";
        private static readonly string TestFaloopInputLine = @"Raiden [S]: Gamma - Yanxia ( 23.6, 11.4 )";
        private static readonly string TestBearInputLine = @"The Rak'tika Greatwood ( 14.6 , 22.3 ) Supay";
        private static readonly string TestSirenInstancedInputLine = $@"(Yilan) {LinkChar}Thavnair{I2Char} ( 26.8  , 20.9 )  (Instance TWO) ";
        private static readonly string TestFaloopInstancedInputLine = @"Odin [S]: Vogaal Ja - Middle La Noscea (1) ( 8.2, 32.65 )";
        private static readonly string TestBearInstancedInputLine = @"Thavnair 3 ( 27.6 , 25.6 ) Sugriva";
        private static readonly MarkData TestSirenMarkData =
            new MarkData("Stolas", "The Dravanian Hinterlands", 362, 712, null, new Vector2(26.85f, 20.05f));
        private static readonly MarkData TestFaloopMarkData =
            new MarkData("Gamma", "Yanxia", 14, 1343, null, new Vector2(23.6f, 11.4f));
        private static readonly MarkData TestBearMarkData =
            new MarkData("Supay", "The Rak'tika Greatwood", 3067, 32, null, new Vector2(14.6f, 22.3f));
        private static readonly MarkData TestSirenInstancedMarkData =
            new MarkData("Yilan", "Thavnair", 5362, 15, 2, new Vector2(26.8f, 20.9f));
        private static readonly MarkData TestFaloopInstancedMarkData =
            new MarkData("Vogaal Ja", "Middle La Noscea", 83, 433, 1, new Vector2(8.2f, 32.65f));
        private static readonly MarkData TestBearInstancedMarkData =
            new MarkData("Sugriva", "Thavnair", 316, 2283, 3, new Vector2(27.6f, 25.6f));
        private static readonly IReadOnlyList<string> TestInputLines = new[]
        {
            TestSirenInputLine,
            TestFaloopInputLine,
            TestBearInputLine,
            TestSirenInstancedInputLine,
            TestFaloopInstancedInputLine,
            TestBearInstancedInputLine
        }.ToImmutableList();

        [SetUp]
        public void Setup()
        {
            Plugin.Chat = Substitute.For<IChatGui>();
            Plugin.Logger = Substitute.For<IPluginLog>();
            Plugin.DataManagerManager = Substitute.For<IDataManagerManager>();
        }

        [Test] public void SirenCanParse() =>
            TestCanParse(ParserType.Siren, 0, 3);

        [Test] public void FaloopCanParse() =>
            TestCanParse(ParserType.Faloop, 1, 4);

        [Test] public void BearCanParse() =>
            TestCanParse(ParserType.Bear, 2, 5);

        private void TestCanParse(ParserType parserType, params int[] trueIndecies)
        {
            // DATA
            var parser = GetParser(parserType);
            var expected = TestInputLines.Select(_ => false).ToList();
            trueIndecies.ForEach(index => expected[index] = true);
            
            // WHEN
            var actual = TestInputLines.Select(parser.CanParseLine).ToList();
            
            // THEN
            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [Test] public void SirenParsesSuccessfully() =>
            TestParsesSuccessfully(ParserType.Siren, TestSirenInputLine, TestSirenMarkData);

        [Test] public void FaloopParsesSuccessfully() =>
            TestParsesSuccessfully(ParserType.Faloop, TestFaloopInputLine, TestFaloopMarkData);

        [Test] public void BearParsesSuccessfully() =>
            TestParsesSuccessfully(ParserType.Bear, TestBearInputLine, TestBearMarkData);

        [Test] public void SirenParsesInstanceSuccessfully() =>
            TestParsesSuccessfully(ParserType.Siren, TestSirenInstancedInputLine, TestSirenInstancedMarkData);

        [Test] public void FaloopParsesInstanceSuccessfully() =>
            TestParsesSuccessfully(ParserType.Faloop, TestFaloopInstancedInputLine, TestFaloopInstancedMarkData);

        [Test] public void BearParsesInstanceSuccessfully() =>
            TestParsesSuccessfully(ParserType.Bear, TestBearInstancedInputLine, TestBearInstancedMarkData);

        private void TestParsesSuccessfully(ParserType parserType, string inputLine, MarkData expected)
        {
            // DATA
            var parser = GetParser(parserType);
            
            // GIVEN
            Plugin.DataManagerManager.GetMapDataByName(Arg.Any<string>())
                .Returns(new MapData(expected.TerritoryId, expected.MapId));
            
            // WHEN
            var actual = parser.Parse(inputLine);
            
            // THEN
            Plugin.DataManagerManager.Received(Quantity.Exactly(1)).GetMapDataByName(expected.MapName);
            
            Assert.That(actual, Is.EqualTo(Result.Success<MarkData, string>(expected)));
        }

        public enum ParserType
        {
            Siren,
            Faloop,
            Bear,
        }

        private static ITrackerParser GetParser(ParserType parserType) =>
            parserType switch
            {
                ParserType.Siren => new SirenParser(),
                ParserType.Faloop => new FaloopParser(),
                ParserType.Bear => new BearParser(),
                _ => throw new Exception($"Unknown parser: {parserType}")
            };
    }
}
