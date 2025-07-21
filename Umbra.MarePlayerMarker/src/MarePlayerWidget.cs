using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Umbra.Common;
using Umbra.Game;
using Umbra.Widgets;
using System;
using Dalamud.Game.ClientState.Objects.Enums;

namespace Umbra.MarePlayerMarker;

[ToolbarWidget(
    "Umbra_MarePlayerWidget",
    "Mare同步玩家组件",
    "显示一个清单显示通过Mare与你同步的玩家。"
)]
public class MarePlayerWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    public override MenuPopup Popup { get; } = new();

    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.Icon;

    private readonly Dictionary<string, Dictionary<string, MenuPopup.Button>> _menuItems   = [];
    private readonly MenuPopup.Group                                          _playerGroup = new("同步玩家");

    private MarePlayerRepository Repository    { get; } = Framework.Service<MarePlayerRepository>();
    private IPlayer              Player        { get; } = Framework.Service<IPlayer>();
    private ITargetManager       TargetManager { get; } = Framework.Service<ITargetManager>();

    private DateTime _lastUpdateTime = DateTime.MinValue;
    // 玩家缓存结构，包含本地可见状态和距离
    private class CachedPlayerInfo
    {
        public IGameObject? Player { get; set; }
        public ulong GameObjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsLocallyVisible { get; set; }
        public float Distance { get; set; }
    }
    private readonly Dictionary<ulong, CachedPlayerInfo> _cachedPlayers = new();

    protected override void OnLoad()
    {
        Popup.Add(_playerGroup);
        _menuItems["同步玩家"] = [];
    }

    protected override void OnDraw()
    {
        var now = DateTime.Now;
        float interval = GetConfigValue<float>("UpdateIntervalSeconds");
        if (interval < 0.05f) interval = 0.05f;
        if ((now - _lastUpdateTime).TotalSeconds >= interval)
        {
            var players = Repository.GetSyncedPlayers();
            var localPlayer = Player;
            var updatedIds = new HashSet<ulong>();
            foreach (var obj in players)
            {
                if (obj == null) continue;
                var id = obj.GameObjectId;
                bool isLocallyVisible = obj.IsValid()
                    && obj.ObjectKind == ObjectKind.Player
                    && !string.IsNullOrEmpty(obj.Name?.TextValue)
                    && obj.Position != Vector3.Zero;
                float distance = isLocallyVisible ? Vector3.Distance(localPlayer.Position, obj.Position) : -1f;
                if (!_cachedPlayers.TryGetValue(id, out var info))
                {
                    info = new CachedPlayerInfo
                    {
                        Player = obj,
                        GameObjectId = id,
                        Name = obj.Name?.TextValue ?? string.Empty,
                    };
                    _cachedPlayers[id] = info;
                }
                info.Player = obj;
                info.IsLocallyVisible = isLocallyVisible;
                info.Distance = distance;
                info.Name = obj.Name?.TextValue ?? string.Empty;
                updatedIds.Add(id);
            }
            // 不在本次同步列表中的玩家，保留但标记为不可见
            foreach (var kv in _cachedPlayers)
            {
                if (!updatedIds.Contains(kv.Key))
                {
                    kv.Value.IsLocallyVisible = false;
                    kv.Value.Distance = -1f;
                }
            }
            _lastUpdateTime = now;
        }
        var playerList = _cachedPlayers.Values.ToList();
        bool isEmpty = playerList.Count == 0;
        bool useUnicode = GetConfigValue<bool>("UseUnicodeIcon");
        var iconId = playerList.Count > 0 ? (uint)GetConfigValue<int>("IconId") : 0u;
        SetGameIconId(iconId);
        IsVisible = !(isEmpty && GetConfigValue<bool>("HideIfEmpty"));
        if (!IsVisible) return;
        if (playerList.Count == 0)
        {
            if (useUnicode)
            {
                SetText("\uE044 0");
            }
            else
            {
                SetText(" 0");
                ClearIcon();
            }
            return;
        }
        if (useUnicode)
        {
            SetText($"\uE044 {playerList.Count}");
        }
        else
        {
            SetText($" {playerList.Count}");
        }
        UpdateMenuItems(playerList, _playerGroup);
    }

    // 传入CachedPlayerInfo列表
    private void UpdateMenuItems(List<CachedPlayerInfo> list, MenuPopup.Group group)
    {
        if (!_menuItems.ContainsKey(group.Label!)) _menuItems[group.Label!] = [];
        List<string> usedIds = [];
        foreach (var info in list)
        {
            var id = $"obj_{info.GameObjectId}";
            string label = info.Name;
            string dist = info.IsLocallyVisible ? $"{info.Distance:N0} 米" : "不可见";
            usedIds.Add(id);
            if (!_menuItems[group.Label!].ContainsKey(id))
            {
                _menuItems[group.Label!][id] = new MenuPopup.Button(label)
                {
                    OnClick = () => {
                        var obj = info.Player;
                        if (obj == null || !info.IsLocallyVisible) return;
                        TargetManager.Target = obj;
                    },
                };
            }
            var button = _menuItems[group.Label!][id];
            // 每次都更新 OnClick，确保引用最新的 info.Player
            button.OnClick = () => {
                var obj = info.Player;
                if (obj == null || !info.IsLocallyVisible) return;
                TargetManager.Target = obj;
            };
            button.IsDisabled = !info.IsLocallyVisible || info.Distance > 50;
            button.Icon       = null;
            button.AltText    = dist;
            button.SortIndex  = info.Player?.ObjectIndex ?? 0;
            group.Add(button);
        }
        foreach (var (id, btn) in _menuItems[group.Label!].ToDictionary())
        {
            if (!usedIds.Contains(id))
            {
                group.Remove(btn);
                _menuItems[group.Label!].Remove(id);
            }
        }
    }

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return
        [
            ..base.GetConfigVariables(),
            new BooleanWidgetConfigVariable(
                "HideIfEmpty",
                "如果没有同步玩家，则隐藏组件。",
                "如果当前没有通过Mare同步的玩家，隐藏组件。",
                true
            ),
            new BooleanWidgetConfigVariable(
                "UseUnicodeIcon",
                "使用Unicode图标",
                "使用Unicode字符作为图标，而不是使用图标ID。",
                true
            ),
            new IntegerWidgetConfigVariable(
                "IconId",
                "同步玩家图标ID",
                "用于组件的图标ID。使用值0可禁用图标。输入\"/xldata icons\"到聊天框中以访问图标浏览器。仅在未使用Unicode图标时有效。",
                63936
            ),
            new FloatWidgetConfigVariable(
                "UpdateIntervalSeconds",
                "更新间隔 (秒)",
                "组件刷新间隔，支持小数，最小0.05。",
                1.0f
            ),
        ];
    }
} 