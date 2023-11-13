using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace CoordImporter.Managers;

public class HuntHelperManager : IDisposable
{
    private const uint SupportedVersion = 1;

    private readonly ICallGateSubscriber<uint> cgGetVersion;
    private readonly ICallGateSubscriber<uint, bool> cgEnable;
    private readonly ICallGateSubscriber<bool> cgDisable;
    private readonly ICallGateSubscriber<List<TrainMark>, bool> cgImportTrainList;

    public bool Available { get; private set; }

    public HuntHelperManager()
    {
        cgGetVersion = Plugin.PluginInterface.GetIpcSubscriber<uint>("HH.GetVersion");
        cgEnable = Plugin.PluginInterface.GetIpcSubscriber<uint, bool>("HH.Enable");
        cgDisable = Plugin.PluginInterface.GetIpcSubscriber<bool>("HH.Disable");
        cgImportTrainList = Plugin.PluginInterface.GetIpcSubscriber<List<TrainMark>, bool>("HH.ImportTrainList");

        CheckVersion();
        cgEnable.Subscribe(OnEnable);
        cgDisable.Subscribe(OnDisable);
    }

    public void Dispose()
    {
        cgEnable.Unsubscribe(OnEnable);
        cgDisable.Unsubscribe(OnDisable);
    }

    private void OnEnable(uint version) => CheckVersion(version);

    private void OnDisable()
    {
        Plugin.Logger.Info("Hunt Helper IPC has been disabled. Disabling support.");
        Available = false;
    }

    private void CheckVersion(uint? version = null)
    {
        try
        {
            version ??= cgGetVersion.InvokeFunc();
            if (version == SupportedVersion)
            {
                Plugin.Logger.Info("Hunt Helper IPC version {0} detected. Enabling support.", version);
                Available = true;
            }
            else
            {
                Plugin.Logger.Warning(
                    "Hunt Helper IPC version {0} required, but version {1} detected. Disabling support.",
                    SupportedVersion,
                    version
                );
                Available = false;
            }
        }
        catch (IpcNotReadyError e)
        {
            Plugin.Logger.Info("Hunt Helper is not yet available. Disabling support until it is.");
            Available = false;
        }
    }

    public Maybe<string> ImportTrainList(IEnumerable<MarkData> marks) =>
        ExecuteIpcAction(
            () => cgImportTrainList
                .InvokeAction(
                    marks
                        .Select(ToTrainMark)
                        .Choose()
                        .ToList()
                )
        );

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
            Plugin.Logger.Warning(
                "Hunt Helper appears to have disappeared ;-;. Can't complete the operation ;-;. Disabling support until it comes back."
            );
            Available = false;
            return "Hunt Helper has disappeared from my sight ;-;";
        }
        catch (IpcError e)
        {
            const string message = "Hmm...something unexpected happened communicating with Hunt Helper :T";
            Plugin.Logger.Error(e, message);
            return message;
        }

        return Maybe.None;
    }

    private static Maybe<TrainMark> ToTrainMark(MarkData markData) =>
        Plugin
            .DataManagerManager.GetMobIdByName(markData.MarkName)
            .Or(
                () =>
                {
                    Plugin.Logger.Warning("Could not find MobId for hunt mark: {0}", markData.MarkName);
                    Plugin.Chat.PrintError($"Skipping mark [{markData.MarkName}] -- could not find its MobId ;-;");
                    return Maybe.None;
                }
            )
            .Select(
                mobId => new TrainMark(
                    markData.MarkName,
                    mobId!,
                    markData.TerritoryId,
                    markData.MapId,
                    markData.Instance ?? 0,
                    markData.Position,
                    false,
                    DateTime.Now.ToUniversalTime()
                )
            );

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
