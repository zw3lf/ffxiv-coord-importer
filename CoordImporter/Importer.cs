using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CoordImporter;

public class Importer
{
    public Importer(IEnumerable<ITrackerParser> parsers)
    {
        Parsers = parsers.ToImmutableList();
    }

    private IList<ITrackerParser> Parsers;

    public IEnumerable<Result<MarkData, string>> ParsePayload(string payload) =>
        payload
            .Split(
                new string[] { "\r\n", "\r", "\n" },
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(inputLine => Parsers
                                 .FirstOrDefault(parser => parser.CanParseLine(inputLine))
                                 .ToResult($"Format not recognized for input: {inputLine}")
                                 .Bind(parser => parser.Parse(inputLine))
            )
            .ToImmutableList();
}
