using System.Numerics;
using CoordImporter.Managers;
using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReceivedExtensions;
using NUnit.Framework.Internal;

namespace CoordImporter.Tests;

[TestFixture]
public class ImporterTests
{
    private static readonly string LinkChar = SeIconChar.LinkMarker.ToIconString();
    private static readonly string I2Char = SeIconChar.Instance2.ToIconString();
    private static readonly string I3Char = SeIconChar.Instance3.ToIconString();
    
    private readonly Randomizer randomizer = Randomizer.CreateRandomizer();
    
    private IPluginLog pluginLog = null!;
    private IDataManagerManager dataManagerManager = null!;
    private Importer importer = null!;

    [SetUp]
    public void SetUp()
    {
        pluginLog = Substitute.For<IPluginLog>();
        dataManagerManager = Substitute.For<IDataManagerManager>();
        
        var parsers = new List<ITrackerParser>
        {
            new SirenParser(pluginLog, dataManagerManager),
            new FaloopParser(pluginLog, dataManagerManager),
            new BearParser(pluginLog, dataManagerManager),
        };

        importer = new Importer(pluginLog, parsers);
    }
    
    [Test]
    public void EmptyPayload()
    {
        // WHEN
        var actual = importer.ParsePayload("");
        
        // THEN
        Assert.That(actual, Is.Empty);
    }

    [Test]
    public void SingleMarkSiren()
    {
        TestSingleMarkSuccess(
            $@"(Maybe: Stolas) {LinkChar}The Dravanian Hinterlands ( 26.85  , 20.05 ) ",
            "Stolas", "The Dravanian Hinterlands", null, 26.85f, 20.05f
        );
    }

    [Test]
    public void SingleMarkFaloop()
    {
        TestSingleMarkSuccess(
            @"Raiden [S]: Gamma - Yanxia ( 23.6, 11.4 )",
            "Gamma", "Yanxia", null, 23.6f, 11.4f
        );
    }

    [Test]
    public void SingleMarkBear()
    {
        TestSingleMarkSuccess(
            @"The Rak'tika Greatwood ( 14.6 , 22.3 ) Supay",
            "Supay", "The Rak'tika Greatwood", null, 14.6f, 22.3f
        );
    }

    [Test]
    public void SingleMarkSirenInstanced()
    {
        TestSingleMarkSuccess(
            $@"(Yilan) {LinkChar}Thavnair{I2Char} ( 26.8  , 20.9 )  (Instance TWO) ",
            "Yilan", "Thavnair", 2, 26.8f, 20.9f
        );
    }

    [Test]
    public void SingleMarkFaloopInstanced()
    {
        TestSingleMarkSuccess(
            @"Odin [S]: Vogaal Ja - Middle La Noscea (1) ( 8.2, 32.65 )",
            "Vogaal Ja", "Middle La Noscea", 1, 8.2f, 32.65f
        );
    }

    [Test]
    public void SingleMarkBearInstanced()
    {
        TestSingleMarkSuccess(
            @"Garlemald 3 ( 27.6 , 25.6 ) Sugriva",
            "Sugriva", "Garlemald", 3, 27.6f, 25.6f
        );
    }

    private void TestSingleMarkSuccess(string inputLine, string markName, string mapName, uint? instance, float x, float y)
    {
        // DATA
        var markData = CreateTestMarkData(markName, mapName, instance, x, y);
        var expected = new List<Result<MarkData, string>> { MarkDataResult(markData) };
        
        // GIVEN
        dataManagerManager.GetMapDataByName(Arg.Any<string>())
            .Returns(new MapData(markData.TerritoryId, markData.MapId));

        // WHEN
        var actual = importer.ParsePayload(inputLine);

        // THEN
        dataManagerManager.Received(1).GetMapDataByName(mapName);
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void SingleMarkParsingError()
    {
        // DATA
        var parser = Substitute.For<ITrackerParser>();
        var inputLine = "no thank you ^w^";
        var error = Result.Failure<MarkData, string>("you know, i just don't like this input :3");
        var expected = new[] { error };
        
        // GIVEN
        importer = new Importer(pluginLog, new[] { parser });
        parser.CanParseLine(Arg.Any<string>()).Returns(true);
        parser.Parse(Arg.Any<string>()).Returns(error);

        // WHEN
        var actual = importer.ParsePayload(inputLine);

        // THEN
        parser.Received(1).CanParseLine(inputLine);
        parser.Received(1).Parse(inputLine);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void SingleMarkParsingException()
    {
        // DATA
        var parser = Substitute.For<ITrackerParser>();
        var inputLine = "parser no parsing";
        var expected = new[]
        {
            Result.Failure<MarkData, string>($"An unexpected error occurred while parsing input: {inputLine}")
        };
        
        // GIVEN
        importer = new Importer(pluginLog, new[] { parser });
        parser.CanParseLine(Arg.Any<string>()).Returns(true);
        parser.Parse(Arg.Any<string>()).Throws(new Exception(";-;"));

        // WHEN
        var actual = importer.ParsePayload(inputLine);

        // THEN
        parser.Received(1).CanParseLine(inputLine);
        parser.Received(1).Parse(inputLine);
        pluginLog.Received(1).Error(Arg.Any<Exception?>(), Arg.Any<string>(), Arg.Any<object[]>());
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void SingleMarkNoParser()
    {
        // DATA
        var parser = Substitute.For<ITrackerParser>();
        var inputLine = @"¯\_(ツ)_/¯";
        var expected = new[]
        {
            Result.Failure<MarkData, string>($"Format not recognized for input: {inputLine}")
        };
        
        // GIVEN
        importer = new Importer(pluginLog, new[] { parser });
        parser.CanParseLine(Arg.Any<string>()).Returns(false);

        // WHEN
        var actual = importer.ParsePayload(inputLine);

        // THEN
        parser.Received(1).CanParseLine(inputLine);
        parser.Received(Quantity.None()).Parse(Arg.Any<string>());
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void MultipleMarks()
    {
        // DATA
        var payload = $@"
            Kholusia ( 22.2 , 14.1 ) Lil Murderer
            Amh Araeng ( 28.7 , 20.3 ) Maliktender
            (Maybe: Hulder) {LinkChar}Labyrinthos ( 32.3  , 25.9 ) 
            The Tempest ( 29.1 , 22.9 ) Rusalka
            (Yilan) {LinkChar}Thavnair{I3Char} ( 14.3  , 12.2 )  (Instance THREE)
            (Aegeiros) {LinkChar}Garlemald ( 23.4  , 25.8 ) 
        ";
        var markDatas = new List<MarkData>
        {
            CreateTestMarkData("Lil Murderer", "Kholusia"   , null, 22.2f, 14.1f),
            CreateTestMarkData("Maliktender" , "Amh Araeng" , null, 28.7f, 20.3f),
            CreateTestMarkData("Hulder"      , "Labyrinthos", null, 32.3f, 25.9f),
            CreateTestMarkData("Rusalka"     , "The Tempest", null, 29.1f, 22.9f),
            CreateTestMarkData("Yilan"       , "Thavnair"   , 3   , 14.3f, 12.2f),
            CreateTestMarkData("Aegeiros"    , "Garlemald"  , null, 23.4f, 25.8f),
        };
        var expected = markDatas.Select(MarkDataResult).ToList();
        var mapData = markDatas
            .Select(markData => new MapData(markData.TerritoryId, markData.MapId))
            .Select(Maybe.From)
            .ToList();
        
        // GIVEN
        dataManagerManager.GetMapDataByName(Arg.Any<string>())
            .Returns(mapData[0], mapData.TakeLast(markDatas.Count - 1).ToArray());

        // WHEN
        var actual = importer.ParsePayload(payload);

        // THEN
        markDatas.ForEach(markData => dataManagerManager.Received(1).GetMapDataByName(markData.MapName));
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    private Result<MarkData, string> MarkDataResult(string markName, string mapName, uint? instance, float x, float y)
    {
        return MarkDataResult(CreateTestMarkData(markName, mapName, instance, x, y));
    }

    private Result<MarkData, string> MarkDataResult(MarkData markData)
    {
        return Result.Success<MarkData, string>(markData);
    }

    private MarkData CreateTestMarkData(string markName, string mapName, uint? instance, float x, float y) =>
        new(
            markName,
            mapName,
            randomizer.NextUInt(1 << 4, 1 << 16),
            randomizer.NextUInt(1 << 4, 1 << 16),
            instance,
            new Vector2(x, y)
        );
}


