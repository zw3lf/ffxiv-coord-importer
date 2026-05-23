using System.Linq;
using System.Text.RegularExpressions;
using CoordImporter.Managers;
using CoordImporter.Models;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace CoordImporter.Parsers;

public class BearOrUltimaParser : Parser
{
    private static readonly char LinkChar = SeIconChar.LinkMarker.ToIconChar();
    
    // For the format "Labyrinthos ( 16.5 , 16.8 ) Storsie"
    // Also supports "Thavnair (27.6, 25.6) 2 Yilan" and "Thavnair 2 (27.6, 25.6) Yilan" (the latter is Ultimahunts's format,
    // which needs to be combined with Bear because the instance-less format is identical and therefore indiscernible from Bear)
    protected readonly Regex Regex = new Regex(
        @"(?<map_name>[\w'\s-^@]*?[\D'\s-^@]+)\s*(?<instance>[1-9]?)\s*\(\s*((?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)|NOT AVAILABLE)\s*\)\s*(?<instance>[1-9]?)\s*(?<mark_name>\w[\w '-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override Result<MarkData, string> Parse(string inputLine)
    {
        var groups = Regex.Matches(inputLine).First().Groups;
        LogParse("BearOrUltima", inputLine, groups);
        if (groups["x_coord"].Value.Length == 0) return ITrackerParser.ParseErrorIgnoreMark;
        return CreateMark(groups, instance => (uint)(instance.First() - '0'));
    }

    public override bool CanParseLine(string inputLine) => !inputLine.Contains(LinkChar) && Regex.IsMatch(inputLine);

    public BearOrUltimaParser(IPluginLog logger, IDataManagerManager dataManagerManager) : base(logger, dataManagerManager) { }
}
