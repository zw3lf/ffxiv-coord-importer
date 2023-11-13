using System.Linq;
using System.Text.RegularExpressions;
using CoordImporter.Managers;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace CoordImporter.Parser;

public class SirenParser : Parser
{
    private static readonly char LinkChar = SeIconChar.LinkMarker.ToIconChar();
    private static readonly char I1Char = SeIconChar.Instance1.ToIconChar();
    private static readonly char I9Char = SeIconChar.Instance9.ToIconChar();
    // For the format "(Maybe: Storsie) {LinkChar}Labyrinthos{I1Char} ( 17  , 9.6 ) "

    protected readonly Regex Regex = new Regex(
        $@"\((Maybe: )?(?<mark_name>[\w '-]+)\) {LinkChar}(?<map_name>[\w '-]+)(?<instance>[{I1Char}-{I9Char}]?)\s+\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)",
        RegexOptions.Compiled);


    public override Result<MarkData, string> Parse(string inputLine)
    {
        var groups = Regex.Matches(inputLine).First().Groups;
        LogParse("Siren", inputLine, groups);
        return CreateMark(groups, instance => (uint)(instance.First() - I1Char) + 1);
    }

    public override bool CanParseLine(string inputLine) => Regex.IsMatch(inputLine);

    public SirenParser(IPluginLog logger, IDataManagerManager dataManagerManager) : base(logger, dataManagerManager) { }
}
