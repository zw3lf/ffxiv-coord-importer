using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CoordImporter.Parser;
using CSharpFunctionalExtensions;

namespace CoordImporter;

public class Importer
{
    private static readonly IList<ITrackerParser> Parsers = new List<ITrackerParser>
    {
        new SirenParser(),
        new FaloopParser(),
        new BearParser(),
    }.ToImmutableList();

    public static IEnumerable<Result<MarkData, string>> ParsePayload(string payload) => payload
        .Split(
            new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        )
        .Select(
            inputLine => Parsers
                .FirstOrDefault(parser => parser.CanParseLine(inputLine))
                .ToResult($"Format not recognized for input: {inputLine}")
                .Bind(parser => parser.Parse(inputLine))
        )
        .ToImmutableList();
}
