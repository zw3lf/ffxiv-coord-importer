using System.Numerics;

namespace CoordImporter.Parser;

public record struct MarkData(
    string MarkName,
    string MapName,
    uint TerritoryId,
    uint MapId,
    uint? Instance,
    Vector2 Position
);
