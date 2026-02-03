using System.Collections.Generic;
using Dalamud.Plugin.Services;
using XIVHuntUtils.Managers;
using XIVHuntUtils.Models;

namespace CoordImporter.Managers;

public class InitializationManager
{
    private readonly IPluginLog _log;
    private readonly ITerritoryManager _territoryManager;

    public InitializationManager(
        IPluginLog log,
        ITerritoryManager territoryManager,
        // these are unused, but including them in the constructor forces them to be
        // initialized right away, rather than waiting for a dependant to be used.
        IMobManager mobManager
    )
    {
        _log = log;
        _territoryManager = territoryManager;
    }

    public void InitializeNecessaryComponents()
    {
        InitializeTerritoryInstances();
    }

    private void InitializeTerritoryInstances() =>
        TerritoryExtensions.SetTerritoryInstances(new Dictionary<uint, uint>(), _territoryManager.GetTerritoryIds());
}
