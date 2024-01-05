using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dalamud.Plugin.Services;

namespace CoordImporter;

public class Importer
{
    private readonly IPluginLog logger;
    private readonly IList<ITrackerParser> parsers;
    
    public Importer(IPluginLog logger, IEnumerable<ITrackerParser> parsers)
    {
        this.logger = logger;
        this.parsers = parsers.ToImmutableList();
    }

    public IEnumerable<Result<MarkData, string>> ParsePayload(string payload) =>
        payload
            .Split(
                new string[] { "\r\n", "\r", "\n" },
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(inputLine => parsers
                                 .FirstOrDefault(parser => parser.CanParseLine(inputLine))
                                 .ToResult($"Format not recognized for input: {inputLine}")
                                 .Bind(parser =>
                                 {
                                     try
                                     {
                                         return parser.Parse(inputLine);
                                     }
                                     catch (Exception e)
                                     {
                                         var message = $"An unexpected error occurred while parsing input: {inputLine}";
                                         logger.Error(e, message);
                                         return message;
                                     }
                                 })
            )
            .Where(result => !ITrackerParser.ParseErrorIgnoreMark.Equals(result))
            .ToImmutableList();
}
