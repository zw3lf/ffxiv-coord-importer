using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CoordImporter.Managers;

public class HuntHelperManager : IDisposable
{
    private const uint SupportedVersion = 1;

    private readonly ICallGateSubscriber<uint> cgGetVersion;
    private readonly ICallGateSubscriber<uint, bool> cgEnable;
    private readonly ICallGateSubscriber<bool> cgDisable;
    private readonly ICallGateSubscriber<List<TrainMark>, bool> cgImportTrainList;

    private IPluginLog Logger { get; init; }
    private IDataManagerManager DataManagerManager { get; init; }
    private IChatGui Chat { get; init; }

    public bool Available { get; private set; } = false;

    public HuntHelperManager(DalamudPluginInterface pluginInterface, IPluginLog logger, IDataManagerManager dataManagerManager, IChatGui chat)
    {
        Logger = logger;
        DataManagerManager = dataManagerManager;
        Chat = chat;
        cgGetVersion = pluginInterface.GetIpcSubscriber<uint>("HH.GetVersion");
        cgEnable = pluginInterface.GetIpcSubscriber<uint, bool>("HH.Enable");
        cgDisable = pluginInterface.GetIpcSubscriber<bool>("HH.Disable");
        cgImportTrainList = pluginInterface.GetIpcSubscriber<List<TrainMark>, bool>("HH.ImportTrainList");

        CheckVersion();
        cgEnable.Subscribe(OnEnable);
        cgDisable.Subscribe(OnDisable);
    }

    public void Dispose()
    {
        cgEnable.Unsubscribe(OnEnable);
        cgDisable.Unsubscribe(OnDisable);
    }

    private void OnEnable(uint version)
    {
        CheckVersion(version);
    }

    private void OnDisable()
    {
        Logger.Info("Hunt Helper IPC has been disabled. Disabling support.");
        Available = false;
    }

    private void CheckVersion(uint? version = null)
    {
        try
        {
            version ??= cgGetVersion.InvokeFunc();
            if (version == SupportedVersion)
            {
                Logger.Info("Hunt Helper IPC version {0} detected. Enabling support.", version);
                Available = true;
            }
            else
            {
                Logger.Warning(
                    "Hunt Helper IPC version {0} required, but version {1} detected. Disabling support.",
                    SupportedVersion,
                    version
                );
                Available = false;
            }
        }
        catch (IpcNotReadyError e)
        {
            Logger.Info("Hunt Helper is not yet available. Disabling support until it is.");
            Available = false;
        }
    }

    public Maybe<string> ImportTrainList(IEnumerable<MarkData> marks)
    {
        return ExecuteIpcAction(() => cgImportTrainList
                                    .InvokeAction(
                                        marks
                                            .Select(ToTrainMark)
                                            .Choose()
                                            .ToList()
                                    )
        );
    }

    private Maybe<string> ExecuteIpcAction(Action ipcAction)
    {

        if (!Available)
        {
            return "Hunt Helper is not currently available ;-;";
        }

        try
        {
            ipcAction.Invoke();
        }
        catch (IpcNotReadyError e)
        {
            Logger.Warning(
                "Hunt Helper appears to have disappeared ;-;. Can't complete the operation ;-;. Disabling support until it comes back."
            );
            Available = false;
            return "Hunt Helper has disappeared from my sight ;-;";
        }
        catch (IpcError e)
        {
            const string message = "Hmm...something unexpected happened communicating with Hunt Helper :T";
            Logger.Error(e, message);
            return message;
        }

        return Maybe.None;
    }

    private Maybe<TrainMark> ToTrainMark(MarkData markData)
    {
        return DataManagerManager
               .GetMobIdByName(markData.MarkName)
               .Or(() =>
               {
                   Logger.Warning("Could not find MobId for hunt mark: {0}", markData.MarkName);
                   Chat.PrintError($"Skipping mark [{markData.MarkName}] -- could not find its MobId ;-;");
                   return Maybe.None;
               })
               .Select(mobId =>
                           new TrainMark(
                               markData.MarkName,
                               (uint)mobId!,
                               markData.TerritoryId,
                               markData.MapId,
                               markData.Instance ?? 0,
                               markData.Position,
                               false,
                               DateTime.Now.ToUniversalTime()
                           ));
    }

    private record struct TrainMark(
        string Name,
        uint MobId,
        uint TerritoryId,
        uint MapId,
        uint Instance,
        Vector2 Position,
        bool Dead,
        DateTime LastSeenUtc
    );
}
