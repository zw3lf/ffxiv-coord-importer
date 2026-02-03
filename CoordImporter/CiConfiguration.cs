using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Plugin;
using DitzyExtensions;
using DitzyExtensions.Collection;
using XIVHuntUtils.Models;
using static CoordImporter.Managers.SortManager;

namespace CoordImporter;

[Serializable]
public class CiConfiguration : IPluginConfiguration
{
    // the below exists just to make saving less cumbersome
    [NonSerialized]
    private IDalamudPluginInterface pluginInterface = null!;

    public int Version { get; set; } = 0;
    
    public bool PrintOptimalPath = true;

    public bool AetheryteSortInstances = true;

    public bool AetheryteKeepMapsTogether = true;

    public bool AetheryteCompleteMaps = true;

    public List<SortCriteria> ActiveSortOrder = [];

    public List<Patch> PatchSortOrder = [];

    public Dictionary<Patch, List<Territory>> TerritorySortOrder = [];

    public CiConfiguration Initialize(IDalamudPluginInterface? pluginInterface = null)
    {
        if (pluginInterface != null) this.pluginInterface = pluginInterface;

        if (PatchSortOrder.IsNotEmpty()) return this;
        
        ActiveSortOrder = [SortCriteria.Patch, SortCriteria.Map, SortCriteria.Instance, SortCriteria.Aetheryte];

        PatchSortOrder = (EnumExtensions.GetEnumValues<Patch>().Reverse().AsMutableList() as List<Patch>)!;

        TerritorySortOrder = (
            EnumExtensions.GetEnumValues<Patch>()
                    .Select(p => (p, (p.HuntMaps().AsMutableList() as List<Territory>)!))
                    .AsMutableDict()
                as Dictionary<Patch, List<Territory>>
        )!;

        return this;
    }

    public void Save()
    {
        pluginInterface.SavePluginConfig(this);
    }
}
