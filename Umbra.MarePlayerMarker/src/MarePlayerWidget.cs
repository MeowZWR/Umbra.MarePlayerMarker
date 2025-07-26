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

public class MenuButtonManager
{
    private readonly Dictionary<string, Dictionary<string, MenuPopup.Button>> _menuItems = [];
    
    public void EnsureGroupExists(string groupName) 
    {
        if (!_menuItems.ContainsKey(groupName)) 
            _menuItems[groupName] = [];
    }
    
    public MenuPopup.Button GetOrCreateButton(string groupName, string buttonId, string label, Action onClick, int sortIndex = 0)
    {
        EnsureGroupExists(groupName);
        
        if (!_menuItems[groupName].ContainsKey(buttonId))
        {
            _menuItems[groupName][buttonId] = new MenuPopup.Button(label)
            {
                OnClick = onClick,
                SortIndex = sortIndex
            };
        }
        return _menuItems[groupName][buttonId];
    }
    
    public void UpdateButton(MenuPopup.Button button, string label, bool isDisabled = false, string? altText = null, bool closeOnClick = false)
    {
        button.Label = label;
        button.IsDisabled = isDisabled;
        button.AltText = altText;
        button.ClosePopupOnClick = closeOnClick;
    }
    
    public void CleanupUnusedButtons(MenuPopup.Group group, List<string> usedIds)
    {
        var groupName = group.Label!;
        if (!_menuItems.ContainsKey(groupName)) return;
        
        foreach (var (id, btn) in _menuItems[groupName].ToDictionary())
        {
            if (!usedIds.Contains(id))
            {
                group.Remove(btn);
                _menuItems[groupName].Remove(id);
            }
        }
    }
}

public class ToggleButton
{
    public string Id { get; }
    public string OnText { get; }
    public string OffText { get; }
    public int SortIndex { get; }
    public Func<bool> GetValue { get; }
    public Action<bool> SetValue { get; }
    
    public ToggleButton(string id, string onText, string offText, Func<bool> getValue, Action<bool> setValue, int sortIndex = 0)
    {
        Id = id;
        OnText = onText;
        OffText = offText;
        GetValue = getValue;
        SetValue = setValue;
        SortIndex = sortIndex;
    }
    
    public string GetLabel() => GetValue() ? OnText : OffText;
    public void Toggle() => SetValue(!GetValue());
}

internal class CachedPlayerInfo
{
    public IGameObject? Player { get; set; }
    public ulong GameObjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsLocallyVisible { get; set; }
    public float Distance { get; set; }
}

[ToolbarWidget("Umbra_MarePlayerWidget", "Mare同步玩家组件", "提供一个清单显示通过Mare与你同步的玩家。")]
public class MarePlayerWidget(
    WidgetInfo info,
    string? guid = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    public override MenuPopup Popup { get; } = new();
    protected override StandardWidgetFeatures Features => StandardWidgetFeatures.Text | StandardWidgetFeatures.Icon;

    private readonly MenuButtonManager _buttonManager = new();
    private readonly MenuPopup.Group _settingsGroup = new("组件设置");
    private readonly MenuPopup.Group _playerGroup = new("同步玩家");
    
    private readonly MarePlayerRepository _repository = Framework.Service<MarePlayerRepository>();
    private readonly MarePlayerMarker _marker = Framework.Service<MarePlayerMarker>();
    private readonly IPlayer _player = Framework.Service<IPlayer>();
    private readonly ITargetManager _targetManager = Framework.Service<ITargetManager>();

    private DateTime _lastUpdateTime = DateTime.MinValue;
    private string _lastUsedVfxId = "vfx/common/eff/m0487_w3_mark0h.avfx";
    private readonly Dictionary<ulong, CachedPlayerInfo> _cachedPlayers = new();

    private ToggleButton[] _toggleButtons = null!;

    protected override void OnLoad()
    {
        _toggleButtons = [
            new("auto_clear_btn", "自动清理：开", "自动清理：关", 
                () => GetConfigValue<bool>("AutoClearInvisible"),
                val => SetConfigValue("AutoClearInvisible", val), 0),
            new("anonymize_btn", "角色匿名：开", "角色匿名：关",
                () => _marker.GetConfigValue<bool>("AnonymizeName"),
                val => _marker.SetConfigValue("AnonymizeName", val), 1),
            new("vfx_btn", "特效标记：开", "特效标记：关",
                () => !string.IsNullOrEmpty(_marker.GetConfigValue<string>("VfxId")),
                val => ToggleVfx(val), 2)
        ];
        
        Popup.Add(_settingsGroup);
        Popup.Add(_playerGroup);
    }

    protected override void OnDraw()
    {
        UpdatePlayerCache();
        CleanupInvisiblePlayers();
        
        var playerList = _cachedPlayers.Values.ToList();
        var (visibleCount, totalCount) = (playerList.Count(p => p.IsLocallyVisible), playerList.Count);
        
        UpdateWidgetDisplay(totalCount, visibleCount);
        if (!IsVisible) return;

        UpdateSettingsButtons();
        UpdatePlayerButtons(playerList);
    }

    private void UpdatePlayerCache()
    {
        var now = DateTime.Now;
        var interval = Math.Max(0.05f, GetConfigValue<float>("UpdateIntervalSeconds"));
        if ((now - _lastUpdateTime).TotalSeconds < interval) return;

        var players = _repository.GetSyncedPlayers();
        var updatedIds = new HashSet<ulong>();

        foreach (var obj in players)
        {
            if (obj == null) continue;
            
            var id = obj.GameObjectId;
            var isVisible = IsPlayerVisible(obj);
            var distance = isVisible ? Vector3.Distance(_player.Position, obj.Position) : -1f;
            
            if (!_cachedPlayers.TryGetValue(id, out var info))
            {
                info = new CachedPlayerInfo();
                _cachedPlayers[id] = info;
            }
            
            info.Player = obj;
            info.GameObjectId = id;
            info.Name = obj.Name?.TextValue ?? string.Empty;
            info.IsLocallyVisible = isVisible;
            info.Distance = distance;
            updatedIds.Add(id);
        }

        foreach (var kv in _cachedPlayers.Where(kv => !updatedIds.Contains(kv.Key)))
        {
            kv.Value.IsLocallyVisible = false;
            kv.Value.Distance = -1f;
        }

        _lastUpdateTime = now;
    }

    private static bool IsPlayerVisible(IGameObject obj) =>
        obj.IsValid() && obj.ObjectKind == ObjectKind.Player && 
        !string.IsNullOrEmpty(obj.Name?.TextValue) && obj.Position != Vector3.Zero;

    private void CleanupInvisiblePlayers()
    {
        if (!GetConfigValue<bool>("AutoClearInvisible")) return;
        
        var toRemove = _cachedPlayers.Where(kv => !kv.Value.IsLocallyVisible).Select(kv => kv.Key).ToList();
        foreach (var key in toRemove)
            _cachedPlayers.Remove(key);
    }

    private void UpdateWidgetDisplay(int totalCount, int visibleCount)
    {
        var useUnicode = GetConfigValue<bool>("UseUnicodeIcon");
        var iconId = totalCount > 0 ? (uint)GetConfigValue<int>("IconId") : 0u;
        
        SetGameIconId(iconId);
        IsVisible = !(totalCount == 0 && GetConfigValue<bool>("HideIfEmpty"));
        
        if (totalCount == 0)
        {
            SetText(useUnicode ? "\uE044 0/0" : " 0/0");
            if (!useUnicode) ClearIcon();
        }
        else
        {
            SetText(useUnicode ? $"\uE044 {visibleCount}/{totalCount}" : $" {visibleCount}/{totalCount}");
        }
    }

    private void UpdateSettingsButtons()
    {
        var usedIds = new List<string>();
        
        foreach (var toggle in _toggleButtons)
        {
            var button = _buttonManager.GetOrCreateButton(_settingsGroup.Label!, toggle.Id, toggle.GetLabel(), 
                () => { toggle.Toggle(); UpdateSettingsButtons(); }, toggle.SortIndex);
            
            _buttonManager.UpdateButton(button, toggle.GetLabel(), closeOnClick: false);
            _settingsGroup.Add(button);
            usedIds.Add(toggle.Id);
        }
        
        _buttonManager.CleanupUnusedButtons(_settingsGroup, usedIds);
    }

    private void UpdatePlayerButtons(List<CachedPlayerInfo> playerList)
    {
        var usedIds = new List<string>();
        var sortedList = playerList
            .Where(p => p.IsLocallyVisible).OrderBy(p => p.Distance)
            .Concat(playerList.Where(p => !p.IsLocallyVisible).OrderBy(p => p.Player?.ObjectIndex ?? int.MaxValue))
            .ToList();

        if (sortedList.Count == 0)
        {
            CreateEmptyStateButton(usedIds);
        }
        else
        {
            CreatePlayerButtons(sortedList, usedIds);
        }
        
        _buttonManager.CleanupUnusedButtons(_playerGroup, usedIds);
    }

    private void CreateEmptyStateButton(List<string> usedIds)
    {
        const string emptyTipId = "empty_tip";
        var button = _buttonManager.GetOrCreateButton(_playerGroup.Label!, emptyTipId, "暂无同步玩家", () => { });
        _buttonManager.UpdateButton(button, "暂无同步玩家", isDisabled: true);
        _playerGroup.Add(button);
        usedIds.Add(emptyTipId);
    }

    private void CreatePlayerButtons(List<CachedPlayerInfo> sortedList, List<string> usedIds)
    {
        int sortIdx = 0;
        foreach (var info in sortedList)
        {
            var id = $"obj_{info.GameObjectId}";
            var distText = info.IsLocallyVisible ? $"{info.Distance:N0} 米" : "不可见";
            
            var button = _buttonManager.GetOrCreateButton(_playerGroup.Label!, id, info.Name, 
                () => {
                    if (info.Player?.IsValid() == true && info.IsLocallyVisible)
                        _targetManager.Target = info.Player;
                }, sortIdx++);
            
            _buttonManager.UpdateButton(button, info.Name, 
                isDisabled: !info.IsLocallyVisible || info.Distance > 50,
                altText: distText, closeOnClick: true);
            
            _playerGroup.Add(button);
            usedIds.Add(id);
        }
    }

    private void ToggleVfx(bool enable)
    {
        var currentVfxId = _marker.GetConfigValue<string>("VfxId");
        
        if (enable)
        {
            _marker.SetConfigValue("VfxId", _lastUsedVfxId);
        }
        else
        {
            if (!string.IsNullOrEmpty(currentVfxId))
                _lastUsedVfxId = currentVfxId;
            _marker.SetConfigValue("VfxId", "");
        }
    }

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return
        [
            ..base.GetConfigVariables(),
            new BooleanWidgetConfigVariable("HideIfEmpty", "如果没有同步玩家，则隐藏组件。", "如果当前没有通过Mare同步的玩家，隐藏组件。", true),
            new BooleanWidgetConfigVariable("UseUnicodeIcon", "使用Unicode图标", "使用Unicode字符作为图标，而不是使用图标ID。", true),
            new IntegerWidgetConfigVariable("IconId", "同步玩家图标ID", "用于组件的图标ID。使用值0可禁用图标。输入\"/xldata icons\"到聊天框中以访问图标浏览器。仅在未使用Unicode图标时有效。", 63936),
            new FloatWidgetConfigVariable("UpdateIntervalSeconds", "更新间隔 (秒)", "组件刷新间隔，支持小数，最小0.05。", 1.0f),
            new BooleanWidgetConfigVariable("AutoClearInvisible", "自动清理不可见玩家", "开启后，列表会自动移除所有不可见玩家。", false),
        ];
    }
} 