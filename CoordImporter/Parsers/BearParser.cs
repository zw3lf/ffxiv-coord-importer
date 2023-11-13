using System.Linq;
using System.Text.RegularExpressions;
using CoordImporter.Managers;
using CSharpFunctionalExtensions;
using Dalamud.Plugin.Services;

namespace CoordImporter.Parser;

public class BearParser : Parser
{
    // For the format "Labyrinthos ( 16.5 , 16.8 ) Storsie"
    protected readonly Regex Regex = new Regex(
        @"(?<map_name>[\D'\s-]+)\s*(?<instance>[1-9]?)\s*\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)\s*(?<mark_name>\w[\w -]+)",
        RegexOptions.Compiled);

    public override Result<MarkData, string> Parse(string inputLine)
    {
        var groups = Regex.Matches(inputLine).First().Groups;
        LogParse("Bear", inputLine, groups);
        return CreateMark(groups, instance => (uint)(instance.First() - '0'));
    }

    public override bool CanParseLine(string inputLine) => Regex.IsMatch(inputLine);

    public BearParser(IPluginLog logger, IDataManagerManager dataManagerManager) : base(logger, dataManagerManager) { }
}
