using System.Collections.Generic;
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
) : DefaultToolbarWidget(info, guid, configValues)
{
    public override MenuPopup Popup { get; } = new();

    private Dictionary<string, List<string>> _menuItems = [];

    private MarePlayerRepository Repository    { get; } = Framework.Service<MarePlayerRepository>();
    private IPlayer              Player        { get; } = Framework.Service<IPlayer>();
    private ITargetManager       TargetManager { get; } = Framework.Service<ITargetManager>();

    protected override void Initialize()
    {
        Popup.AddGroup("SyncedPlayers", "同步玩家");
        _menuItems["SyncedPlayers"] = [];
    }

    protected override void OnUpdate()
    {
        List<IGameObject> playerList = Repository.GetSyncedPlayers();
        bool              isEmpty    = playerList.Count == 0;

        uint iconId = playerList.Count > 0
            ? (uint)GetConfigValue<int>("IconId")
            : 0u;

        SetIcon(iconId);

        Node.Style.IsVisible = !(isEmpty && GetConfigValue<bool>("HideIfEmpty"));

        if (playerList.Count == 0) {
            SetLabel("没有同步玩家");
            SetIcon(null);
            return;
        }

        SetLabel($"同步玩家：{playerList.Count}");

        UpdateMenuItems(playerList, "SyncedPlayers");

        base.OnUpdate();
    }

    private void UpdateMenuItems(List<IGameObject> list, string group)
    {
        foreach (var obj in list) {
            var   id   = $"obj_{obj.GameObjectId}";
            float d    = Vector3.Distance(Player.Position, obj.Position);
            var   dist = $"{d:N0} 米";

            if (Popup.HasButton(id)) {
                Popup.SetButtonAltLabel(id, dist);
                Popup.SetButtonDisabled(id, d > 50);
                continue;
            }

            _menuItems[group].Add(id);
            Popup.AddButton(
                id,
                obj.Name.TextValue,
                obj.ObjectIndex,
                null,
                dist,
                groupId: group,
                onClick: () => TargetManager.Target = obj
            );
        }

        foreach (string id in _menuItems[group].ToArray()) {
            if (list.Find(obj => $"obj_{obj.GameObjectId}" == id) == null) {
                _menuItems[group].Remove(id);
                Popup.RemoveButton(id);
            }
        }
    }

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return [
            new BooleanWidgetConfigVariable(
                "HideIfEmpty",
                "如果没有同步玩家，则隐藏组件。",
                "如果当前没有通过Mare同步的玩家，隐藏组件。",
                true
            ),
            new IntegerWidgetConfigVariable(
                "IconId",
                "同步玩家图标ID",
                "用于组件的图标ID。使用值0可禁用图标。输入\"/xldata icons\"到聊天框中以访问图标浏览器。",
                63936
            ),
            ..DefaultToolbarWidgetConfigVariables,
            ..SingleLabelTextOffsetVariables
        ];
    }
} 