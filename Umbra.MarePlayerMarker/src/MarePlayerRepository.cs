using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Umbra.Common;
using Umbra.Game;

namespace Umbra.MarePlayerMarker;

[Service]
internal sealed class MarePlayerRepository(
    IClientState clientState,
    IPlayer      player,
    MareIpcService mareIpc
) : IDisposable
{
    private readonly Dictionary<ulong, IGameObject> _syncedPlayers = [];

    public List<IGameObject> GetSyncedPlayers()
    {
        lock (_syncedPlayers) {
            return _syncedPlayers.Values.ToList();
        }
    }

    [OnTick]
    private void OnTick()
    {
        if (null == clientState.LocalPlayer) return;

        lock (_syncedPlayers) {
            if (player.IsBetweenAreas || player.IsInCutscene) {
                _syncedPlayers.Clear();
                return;
            }

            // 从Mare IPC获取同步玩家列表
            var players = mareIpc.GetSyncedPlayers();
            _syncedPlayers.Clear();
            
            foreach (var obj in players)
            {
                _syncedPlayers[obj.GameObjectId] = obj;
            }
        }
    }

    public void Dispose()
    {
        _syncedPlayers.Clear();
    }
} 