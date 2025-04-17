using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Umbra.Common;
using Umbra.CounterSpyPlugin.Interop;

namespace Umbra.MarePlayerMarker;

[Service]
internal sealed class MarePlayerRenderer : IDisposable
{
    private readonly VfxManager _vfx;
    private readonly Dictionary<ulong, nint> _vfxList = [];
    private string _lastVfxId = "";
    private string _currentVfxId = "";

    public MarePlayerRenderer(VfxManager vfx)
    {
        _vfx = vfx;
    }

    public void SetVfxId(string vfxId)
    {
        _currentVfxId = vfxId;
    }

    [OnDraw]
    private void OnDraw()
    {
        if (string.IsNullOrEmpty(_currentVfxId)) return;

        var repository = Framework.Service<MarePlayerRepository>();
        foreach (var obj in repository.GetSyncedPlayers()) {
            if (!_vfxList.ContainsKey(obj.GameObjectId)) {
                SpawnVfx(obj);
            }
        }
    }

    [OnTick]
    private unsafe void OnTick()
    {
        foreach ((ulong id, nint ptr) in _vfxList) {
            var s = (VfxStruct*)ptr;
            if (s == null) continue;

            var obj = (GameObject*)GameObjectManager.Instance()->Objects.GetObjectByGameObjectId(id);
            if (obj == null || string.IsNullOrEmpty(_currentVfxId) || _currentVfxId != _lastVfxId) {
                _vfx.RemoveVfx(ptr);
                _vfxList.Remove(id);
                continue;
            }
        }

        _lastVfxId = _currentVfxId;
    }

    private void SpawnVfx(IGameObject player)
    {
        if (_vfxList.ContainsKey(player.GameObjectId)) return;
        if (false == _currentVfxId.StartsWith("vfx/common/eff/")) return;

        nint ptr = _vfx.PlayVfx(_currentVfxId, player);
        if (ptr == 0) return;

        _vfxList[player.GameObjectId] = ptr;
    }

    public void Dispose()
    {
        foreach (var ptr in _vfxList.Values)
        {
            _vfx.RemoveVfx(ptr);
        }
        _vfxList.Clear();
    }
} 