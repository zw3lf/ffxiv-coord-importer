using System.Linq;
using System.Text.RegularExpressions;
using CoordImporter.Managers;
using CoordImporter.Models;
using CSharpFunctionalExtensions;
using Dalamud.Plugin.Services;

namespace CoordImporter.Parsers;

public class FaloopParser : Parser
{
    // For the format "Raiden [S]: Gamma - Yanxia ( 23.6, 11.4 )"
    protected readonly Regex Regex = new Regex(
        @"(?<world_name>[a-zA-Z0-9'-]+)\s+\[S\]: (?<mark_name>[\w '-]+) - (?<map_name>[\w '-]+)\s+\(?(?<instance>[1-9]?)\)?\s*\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)",
        RegexOptions.Compiled);


    public override Result<MarkData, string> Parse(string inputLine)
    {
        var groups = Regex.Matches(inputLine).First().Groups;
        LogParse("Faloop", inputLine, groups);
        return CreateMark(inputLine, groups, instance => (uint)(instance.First() - '0'));
    }

    public override bool CanParseLine(string inputLine) => Regex.IsMatch(inputLine);

    public FaloopParser(IPluginLog logger, IDataManagerManager dataManagerManager) : base(logger, dataManagerManager) { }
}
