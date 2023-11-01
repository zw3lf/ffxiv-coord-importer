using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using CoordImporter;
using Dalamud.Plugin.Services;
using Lumina;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Moq;

namespace CoordImporter.Tests
{
    [TestFixture]
    public class HappyPathTests
    {
        private Importer _importer;
        private Mock<IChatGui> mockIChatGui;
        private Mock<IPluginLog> mockIPluginLog;
        private IDictionary<string, MapData> testMapDictionary;

        [SetUp]
        public void Setup()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            mockIChatGui = new Mock<IChatGui>();
            mockIPluginLog = new Mock<IPluginLog>();
            testMapDictionary = new Dictionary<string, MapData>
            {
                { "Labyrinthos", new MapData(1, 1) },
                { "The Rak'tika Greatwood", new MapData(2, 2) },
                { "The Lochs", new MapData(3, 3) },
                { "Les Lacs", new MapData(3, 3) },
                { "Das Fenn", new MapData(3, 3) },
                { "ギラバニア湖畔地帯", new MapData(3, 3)}
            };
            _importer = new Importer(mockIChatGui.Object, mockIPluginLog.Object, testMapDictionary);
        }

        private readonly String hulderTestCaseInput = "Labyrinthos ( 12.3 , 45.6 ) Hulder";

        private void ValidateHulderTestCase(MarkInformation data)
        {
            Assert.Multiple(() =>
            {
                Assert.That(data.markName, Is.EqualTo("Hulder"));
                Assert.That(data.map!.TerritoryId, Is.EqualTo(testMapDictionary["Labyrinthos"].TerritoryId));
                Assert.That(data.xCoord, Is.EqualTo(12.3).Within(0.01));
                Assert.That(data.yCoord, Is.EqualTo(45.6).Within(0.01));
                Assert.That(data.instanceId, Is.Null);
            });
        }

        [Test]
        public void SanityTest()
        {
            var parsedPayload = _importer.ParsePayload(hulderTestCaseInput);
            Assert.That(parsedPayload, Has.Count.EqualTo(1));
            ValidateHulderTestCase(parsedPayload[0]);
        }

        [Test]
        public void MultiLineTest()
        {
            var parsedPayload = _importer.ParsePayload($"{hulderTestCaseInput}\nThe Rak'tika Greatwood ( 22.2 , 11.1 ) Grassman");
            Assert.That(parsedPayload, Has.Count.EqualTo(2));
            ValidateHulderTestCase(parsedPayload[0]);
        }

        [Test]
        public void InstanceTest()
        {
            var parsedPayload = _importer.ParsePayload("Labyrinthos 1 ( 12.3 , 45.6 ) Hulder");
            Assert.Multiple(() =>
                {
                    Assert.That(parsedPayload, Has.Count.EqualTo(1));
                    Assert.That(parsedPayload[0].markName, Is.EqualTo("Hulder"));
                    Assert.That(parsedPayload[0].map!.TerritoryId, Is.EqualTo(testMapDictionary["Labyrinthos"].TerritoryId));
                    Assert.That(parsedPayload[0].xCoord, Is.EqualTo(12.3).Within(0.01));
                    Assert.That(parsedPayload[0].yCoord, Is.EqualTo(45.6).Within(0.01));
                    Assert.That(parsedPayload[0].instanceId, Is.EqualTo(Importer.InstanceKeyMap["1"]));
                }
            );
        }

        [Test]
        public void SirenRegexTest()
        {
            var parsedPayload = _importer.ParsePayload("(Maybe: Hulder) \ue0bbLabyrinthos ( 12.3 , 45.6 )");
            Assert.That(parsedPayload, Has.Count.EqualTo(1));
            ValidateHulderTestCase(parsedPayload[0]);
        }

        [Test]
        public void SirenRegexNoMaybeTest()
        {
            var parsedPayload = _importer.ParsePayload("(Hulder) \ue0bbLabyrinthos ( 12.3 , 45.6 )");
            Assert.That(parsedPayload, Has.Count.EqualTo(1));
            ValidateHulderTestCase(parsedPayload[0]);
        }

        [Test]
        public void BearRegexTest()
        {
            var parsedPayload = _importer.ParsePayload(hulderTestCaseInput);
            ValidateHulderTestCase(parsedPayload[0]);
        }

        [Test]
        public void FaloopSRankRegexTest()
        {
            var parsedPayload = _importer.ParsePayload("Twintania [S]: Salt and Light - The Lochs ( 6.4, 8.2 )");
            Assert.Multiple(() =>
            {
                Assert.That(parsedPayload, Has.Count.EqualTo(1));
                Assert.That(parsedPayload[0].markName, Is.EqualTo("Salt and Light"));
                Assert.That(parsedPayload[0].map!.TerritoryId, Is.EqualTo(testMapDictionary["The Lochs"].TerritoryId));
                Assert.That(parsedPayload[0].xCoord, Is.EqualTo(6.4).Within(0.01));
                Assert.That(parsedPayload[0].yCoord, Is.EqualTo(8.2).Within(0.01));
                Assert.That(parsedPayload[0].instanceId, Is.Null);
            });
        }

        [Test]
        public void MapDictionaryResolverTest()
        {
            var parsedPayloadEnglish = _importer.ParsePayload("Twintania [S]: Salt and Light - The Lochs ( 6.4, 8.2 )");
            var parsedPayloadFrench = _importer.ParsePayload("Twintania [S]: Salaclux - Les Lacs ( 6.4, 8.2 )");
            var parsedPayloadGerman = _importer.ParsePayload("Twintania [S]: Salzlicht - Das Fenn ( 6.4, 8.2 )");
            var parsedPayloadJapanese = _importer.ParsePayload("Twintania [S]: ソルト・アンド・ライト - ギラバニア湖畔地帯 ( 6.4, 8.2 )");

            Assert.Multiple(() =>
            {
                Assert.That(parsedPayloadEnglish, Has.Count.EqualTo(parsedPayloadFrench.Count));
                Assert.That(parsedPayloadEnglish[0].xCoord, Is.EqualTo(parsedPayloadFrench[0].xCoord));
                Assert.That(parsedPayloadEnglish[0].yCoord, Is.EqualTo(parsedPayloadFrench[0].yCoord));
                Assert.That(parsedPayloadEnglish[0].instanceId, Is.EqualTo(parsedPayloadFrench[0].instanceId));
                Assert.That(parsedPayloadEnglish[0].map!.TerritoryId, Is.EqualTo(parsedPayloadFrench[0].map!.TerritoryId));
                Assert.That(parsedPayloadEnglish, Has.Count.EqualTo(parsedPayloadGerman.Count));
                Assert.That(parsedPayloadEnglish[0].xCoord, Is.EqualTo(parsedPayloadGerman[0].xCoord));
                Assert.That(parsedPayloadEnglish[0].yCoord, Is.EqualTo(parsedPayloadGerman[0].yCoord));
                Assert.That(parsedPayloadEnglish[0].instanceId, Is.EqualTo(parsedPayloadGerman[0].instanceId));
                Assert.That(parsedPayloadEnglish[0].map!.TerritoryId, Is.EqualTo(parsedPayloadGerman[0].map!.TerritoryId));
                Assert.That(parsedPayloadEnglish, Has.Count.EqualTo(parsedPayloadJapanese.Count));
                Assert.That(parsedPayloadEnglish[0].xCoord, Is.EqualTo(parsedPayloadJapanese[0].xCoord));
                Assert.That(parsedPayloadEnglish[0].yCoord, Is.EqualTo(parsedPayloadJapanese[0].yCoord));
                Assert.That(parsedPayloadEnglish[0].instanceId, Is.EqualTo(parsedPayloadJapanese[0].instanceId));
                Assert.That(parsedPayloadEnglish[0].map!.TerritoryId, Is.EqualTo(parsedPayloadJapanese[0].map!.TerritoryId));
            });
        }

        [Test]
        public void WeirdNameTests()
        {
            var parsedLaHee = _importer.ParsePayload("The Rak'tika Greatwood ( 22.2 , 11.1 ) Grassman");
            var parsedFrenchSouthShroud = _importer.ParsePayload("Forêt du sud ( 1.2, 3.4 ) Flagelleur Mental");
            var parsedFrenchNandussy = _importer.ParsePayload("Haute-Noscea (6.9, 4.20) Nandi");
            var parsedGermanRumi = _importer.ParsePayload("Mare Lamentorum (1.23, 4.56) Grübler");

            Assert.Multiple(() =>
            {
                Assert.That(parsedLaHee[0].mapName, Is.EqualTo("The Rak'tika Greatwood"));
                Assert.That(parsedFrenchSouthShroud[0].mapName, Is.EqualTo("Forêt du sud"));
                Assert.That(parsedFrenchNandussy[0].mapName, Is.EqualTo("Haute-Noscea"));
                Assert.That(parsedGermanRumi[0].markName, Is.EqualTo("Grübler"));
            });
        }
    }
}
