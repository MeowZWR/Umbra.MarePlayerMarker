using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Umbra.Common;
using Umbra.Game;
using Umbra.Widgets;

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

    protected override void OnLoad()
    {
        Popup.Add(_playerGroup);
        _menuItems["同步玩家"] = [];
    }

    protected override void OnDraw()
    {
        List<IGameObject> playerList = Repository.GetSyncedPlayers();
        bool              isEmpty    = playerList.Count == 0;
        bool              useUnicode = GetConfigValue<bool>("UseUnicodeIcon");

        var iconId = playerList.Count > 0
            ? (uint)GetConfigValue<int>("IconId")
            : 0u;

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

    private void UpdateMenuItems(List<IGameObject> list, MenuPopup.Group group)
    {
        if (!_menuItems.ContainsKey(group.Label!)) _menuItems[group.Label!] = [];

        List<string> usedIds = [];

        foreach (var obj in list)
        {
            var   id   = $"obj_{obj.GameObjectId}";
            float d    = Vector3.Distance(Player.Position, obj.Position);
            var   dist = $"{d:N0} 米";

            usedIds.Add(id);

            if (!_menuItems[group.Label!].ContainsKey(id))
            {
                _menuItems[group.Label!][id] = new MenuPopup.Button(obj.Name.TextValue)
                {
                    IsDisabled = d > 50,
                    Icon       = null,
                    AltText    = dist,
                    SortIndex  = obj.ObjectIndex,
                    OnClick    = () => TargetManager.Target = obj,
                };
            }

            var button = _menuItems[group.Label!][id];
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
        ];
    }
} 