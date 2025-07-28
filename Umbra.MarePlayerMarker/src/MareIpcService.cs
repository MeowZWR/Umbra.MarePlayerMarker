using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Umbra.Common;

namespace Umbra.MarePlayerMarker;

[Service]
internal sealed class MareIpcService : IDisposable
{
    private readonly IPluginLog _logger;
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private ICallGateSubscriber<List<nint>>? _getHandledAddresses;
    private ICallGateSubscriber<string, string, string, object?>? _applyStatusesToPairRequest;
    private bool _isInitialized;

    public MareIpcService(
        IPluginLog logger,
        IClientState clientState,
        IObjectTable objectTable
    )
    {
        _logger = logger;
        _clientState = clientState;
        _objectTable = objectTable;
        InitializeIpc();
    }

    private void InitializeIpc()
    {
        try {
            var pluginInterface = Framework.DalamudPlugin;
            _getHandledAddresses = pluginInterface.GetIpcSubscriber<List<nint>>("MareSynchronos.GetHandledAddresses");
            _applyStatusesToPairRequest = pluginInterface.GetIpcSubscriber<string, string, string, object?>("MareSynchronos.ApplyStatusesToMarePlayers");
            _isInitialized = true;
            _logger.Information("Mare IPC subscribers initialized successfully");
        }
        catch (Exception ex) {
            _logger.Warning(ex, "Failed to initialize Mare IPC subscribers, will retry later");
            _isInitialized = false;
        }
    }

    public bool IsEnabled => _isInitialized && _getHandledAddresses != null;

    public IEnumerable<IGameObject> GetSyncedPlayers()
    {
        if (!IsEnabled) {
            if (!_isInitialized) {
                InitializeIpc();
            }
            return [];
        }

        try {
            var handledAddresses = _getHandledAddresses!.InvokeFunc();
            var result = new List<IGameObject>();
            var localPlayer = _clientState.LocalPlayer;

            foreach (var obj in _objectTable) {
                if (obj is not IPlayerCharacter player) continue;
                
                if (localPlayer != null && player.GameObjectId == localPlayer.GameObjectId) {
                    continue;
                }
                
                if (handledAddresses.Contains((nint)player.Address)) {
                    result.Add(player);
                }
            }

            return result;
        }
        catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError) {
            _logger.Warning("Mare IPC is not ready yet, will retry later");
            _isInitialized = false;
            return [];
        }
        catch (Exception ex) {
            _logger.Error(ex, "Failed to get synced players from Mare");
            return [];
        }
    }

    public bool IsPlayerSynced(ulong objectId)
    {
        if (!IsEnabled) {
            if (!_isInitialized) {
                InitializeIpc();
            }
            return false;
        }

        try {
            var obj = _objectTable.SearchById(objectId);
            if (obj is not IPlayerCharacter player) return false;
            
            var localPlayer = _clientState.LocalPlayer;
            if (localPlayer != null && player.GameObjectId == localPlayer.GameObjectId) {
                return false;
            }
            
            var handledAddresses = _getHandledAddresses!.InvokeFunc();
            return handledAddresses.Contains((nint)player.Address);
        }
        catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError) {
            _logger.Warning("Mare IPC is not ready yet, will retry later");
            _isInitialized = false;
            return false;
        }
        catch (Exception ex) {
            _logger.Error(ex, "Failed to check if player is synced with Mare");
            return false;
        }
    }

    public string GetPlayerUid(ulong objectId)
    {
        try {
            var obj = _objectTable.SearchById(objectId);
            if (obj is not IPlayerCharacter player) {
                _logger.Warning("Object {ObjectId} is not a player character", objectId);
                return string.Empty;
            }
            
            // 使用游戏UID
            return player.OwnerId.ToString();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Failed to get player UID");
            return string.Empty;
        }
    }

    public void Dispose()
    {
        // IPC subscribers are managed by Dalamud, no need to dispose them
    }
} 