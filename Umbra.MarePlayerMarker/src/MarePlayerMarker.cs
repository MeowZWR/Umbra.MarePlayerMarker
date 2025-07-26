using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Umbra.Common;
using Umbra.Game;
using Umbra.Markers;

namespace Umbra.MarePlayerMarker;

[Service]
internal sealed class MarePlayerMarker(
    MarePlayerRepository repository,
    MarePlayerRenderer renderer,
    IPlayer player,
    IZoneManager zoneManager,
    MareIpcService mareIpc
) : WorldMarkerFactory
{
    private readonly Dictionary<ulong, string> _playerUids = [];
    private readonly Dictionary<string, string> _anonymizedNames = [];

    public override string Id          => "Umbra_MarePlayerMarker";
    public override string Name        => "Mare同步玩家标记";
    public override string Description => "显示通过Mare与你同步的玩家的世界标记。";

    private string AnonymizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        
        if (_anonymizedNames.TryGetValue(name, out var cachedName))
            return cachedName;
        
        string anonymizedName;
        if (name.Contains(' ')) // 国际服角色名逻辑，有空格
        {
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            anonymizedName = string.Join(" ", parts.Select(p => p.Length > 0 ? $"{p[0]}." : ""));
        }
        else // 国服角色名逻辑，无空格
        {
            anonymizedName = name.Length > 0 ? $"{name[0]}." : name;
        }
        
        _anonymizedNames[name] = anonymizedName;
        return anonymizedName;
    }

    public override List<IMarkerConfigVariable> GetConfigVariables()
    {
        return [
            ..DefaultStateConfigVariables,
            new BooleanMarkerConfigVariable(
                "ShowName",
                "显示名称",
                "在世界标记上显示同步玩家的名字。",
                true
            ),
            new BooleanMarkerConfigVariable(
                "AnonymizeName",
                "匿名",
                "为角色名称打码。",
                false
            ),
            new BooleanMarkerConfigVariable(
                "ShowUid",
                "显示UID",
                "在世界标记上显示玩家的游戏UID。",
                false
            ),
            new BooleanMarkerConfigVariable(
                "UseUnicodeIcon",
                "使用Unicode图标",
                "使用Unicode字符\uE044作为图标，而不是使用图标ID。",
                true
            ),
            new BooleanMarkerConfigVariable(
                "ShowCompassText",
                "在罗盘上显示文本",
                "在罗盘上显示Unicode图标和玩家名称，而不仅仅是图标。",
                true
            ),
            new SelectMarkerConfigVariable(
                "VfxId",
                "特效（开着卸载插件可能炸游戏）",
                "显示在同步玩家身上的视觉效果。（非循环特效会炸游戏，虽然我挑选过了，但说不定验证漏了呢）",
                "无",
                new() {
                    { "", "无" },
                    // 基础效果
                    { "vfx/common/eff/cmrz_castx1c.avfx", "【基础】光" },
                    { "vfx/common/eff/levitate0f.avfx", "【基础】悬浮" },
                    { "vfx/common/eff/dk10ht_cha0h.avfx", "【基础】爱慕者" },
                    
                    // 环绕效果
                    { "vfx/common/eff/dkst_evt01f.avfx", "【环绕】环绕标记1" },
                    { "vfx/common/eff/x6fa_stlp01_c0a1.avfx", "【环绕】环绕标记2" },
                    { "vfx/common/eff/x6fa_stlp02_c0a1.avfx", "【环绕】环绕标记3" },
                    { "vfx/common/eff/m7105_stlp02_c0k1.avfx", "【环绕】大型环绕" },
                    { "vfx/common/eff/m0328sp10st0f.avfx", "【环绕】旋转球" },
                    
                    // 月读效果
                    { "vfx/common/eff/m0487_w3_mark0h.avfx", "【月读】白3层" },
                    { "vfx/common/eff/m0487_w6_mark0h.avfx", "【月读】白6层" },
                    { "vfx/common/eff/m0487_w10_mark0h.avfx", "【月读】白10层" },
                    { "vfx/common/eff/m0487_b3_mark0h.avfx", "【月读】黑3层" },
                    { "vfx/common/eff/m0487_b6_mark0h.avfx", "【月读】黑6层" },
                    { "vfx/common/eff/m0487_b10_mark0h.avfx", "【月读】黑10层" },
                    
                    // 特殊效果
                    { "vfx/common/eff/z6r1_b3_stlp05_c0t1.avfx", "【特殊】棱形牢笼" },
                    { "vfx/common/eff/dk10ht_sdb0c.avfx", "【特殊】地下冒出很多手" },
                    { "vfx/common/eff/dkst_over_p0f.avfx", "【特殊】蓝色光晕" },
                    { "vfx/common/eff/st_akama_kega0j.avfx", "【特殊】红色漩涡" },

                    // 标记
                    { "vfx/common/eff/n4g8_stlp_shlight1v.avfx", "【标记】圣光轰炸" },
                }
            ),
            new IntegerMarkerConfigVariable(
                "IconId",
                "同步玩家图标 ID",
                "用于世界标记的图标 ID。使用值0可禁用图标。在聊天中输入\"/xldata icons\"以访问图标浏览器。",
                63936
            ),
            new IntegerMarkerConfigVariable(
                "MarkerHeight",
                "标记相对于目标的高度",
                "指定世界标记相对于同步玩家位置的高度。值为0时，标记将放置在目标的脚下。",
                2,
                -10,
                10
            ),
            new IntegerMarkerConfigVariable(
                "FadeDistance",
                "消失距离",
                "标记开始消失的距离。",
                10,
                0,
                100
            ),
            new IntegerMarkerConfigVariable(
                "FadeAttenuation",
                "渐隐距离",
                "标记从开始消失到完全消失的距离。",
                5,
                0,
                100
            ),
            new IntegerMarkerConfigVariable(
                "MaxVisibleDistance",
                "最大可见距离",
                "标记的最大可见距离。设置为0表示无限制。",
                0
            )
        ];
    }

    [OnTick]
    private void OnTick()
    {
        try
        {
            if (!zoneManager.HasCurrentZone
                || player.IsBetweenAreas
                || player.IsInCutscene
                || player.IsDead
                || player.IsOccupied
                || !GetConfigValue<bool>("Enabled")
               ) {
                RemoveAllMarkers();
                return;
            }

            renderer.SetVfxId(GetConfigValue<string>("VfxId"));

            List<IGameObject> targets = repository.GetSyncedPlayers();

            if (targets.Count == 0) {
                RemoveAllMarkers();
                return;
            }

            List<string> usedIds = [];

            uint    zoneId           = zoneManager.CurrentZone.Id;
            var     iconId           = (uint)GetConfigValue<int>("IconId");
            var     markerHeight     = GetConfigValue<int>("MarkerHeight");
            var     showName         = GetConfigValue<bool>("ShowName");
            var     anonymizeName    = GetConfigValue<bool>("AnonymizeName");
            var     showUid          = GetConfigValue<bool>("ShowUid");
            var     showOnCompass    = GetConfigValue<bool>("ShowOnCompass");
            var     fadeDistance     = GetConfigValue<int>("FadeDistance");
            var     fadeAttenuation  = GetConfigValue<int>("FadeAttenuation");
            var     maxVisibleDistance = GetConfigValue<int>("MaxVisibleDistance");
            var     useUnicodeIcon   = GetConfigValue<bool>("UseUnicodeIcon");
            var     showCompassText  = GetConfigValue<bool>("ShowCompassText");
            Vector2 fadeDist         = new(fadeDistance, fadeDistance + Math.Max(1, fadeAttenuation));

            // 按Y坐标排序，确保标记从上到下排列
            targets.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));

            // 计算每个玩家的标记位置
            var markerPositions = new Dictionary<ulong, Vector3>();
            foreach (var obj in targets) {
                var basePosition = obj.Position with { Y = obj.Position.Y + markerHeight };
                
                // 检查是否有其他标记在附近
                List<Vector3> nearbyMarkers = markerPositions.Values
                    .Where(p => Vector3.Distance(p, basePosition) < 2.0f)
                    .OrderBy(p => p.Y)
                    .ToList();

                if (nearbyMarkers.Count > 0) {
                    // 如果有其他标记，将新标记放在最高的标记上方
                    basePosition = basePosition with { Y = nearbyMarkers.Last().Y + 1.0f };
                }

                markerPositions[obj.GameObjectId] = basePosition;
            }

            // 更新UID缓存
            var currentPlayerIds = targets.Select(t => t.GameObjectId).ToHashSet();
            _playerUids.Keys.Where(id => !currentPlayerIds.Contains(id)).ToList().ForEach(id => _playerUids.Remove(id));
            
            foreach (var obj in targets) {
                if (!_playerUids.ContainsKey(obj.GameObjectId)) {
                    _playerUids[obj.GameObjectId] = mareIpc.GetPlayerUid(obj.GameObjectId);
                }
            }

            foreach (var obj in targets) {
                var key = $"MarePlayerMarker_{zoneId}_{obj.GameObjectId:x8}";
                usedIds.Add(key);

                string label = "";
                if (showName && showUid) {
                    label = $"{obj.Name.TextValue} ({_playerUids[obj.GameObjectId]})";
                } else if (showName) {
                    label = obj.Name.TextValue;
                } else if (showUid) {
                    label = _playerUids[obj.GameObjectId];
                }

                if (showName && anonymizeName && !string.IsNullOrEmpty(label)) {
                    if (showUid) {
                        int bracketIndex = label.IndexOf(" (");
                        if (bracketIndex > 0) {
                            string name = label[..bracketIndex];
                            string uid = label[bracketIndex..];
                            label = AnonymizeName(name) + uid;
                        } else {
                            label = AnonymizeName(label);
                        }
                    } else {
                        label = AnonymizeName(label);
                    }
                }

                if (useUnicodeIcon) {
                    label = string.IsNullOrEmpty(label) ? "\uE044" : $"\uE044 {label}";
                }

                SetMarker(
                    new() {
                        Key           = key,
                        MapId         = zoneId,
                        IconId        = useUnicodeIcon ? 0u : iconId,
                        Position      = markerPositions[obj.GameObjectId],
                        Label         = label,
                        FadeDistance  = fadeDist,
                        ShowOnCompass = showOnCompass,
                        CompassText   = showOnCompass && showCompassText ? label : null,
                    }
                );
            }

            RemoveMarkersExcept(usedIds);
            
            if (!anonymizeName)
            {
                _anonymizedNames.Clear();
            }
            else if (_anonymizedNames.Count > 0)
            {
                HashSet<string> activeNames = [];
                foreach (var obj in targets)
                {
                    activeNames.Add(obj.Name.TextValue);
                    
                    if (showName && showUid)
                    {
                        activeNames.Add($"{obj.Name.TextValue} ({_playerUids[obj.GameObjectId]})");
                    }
                }
                
                List<string> namesToRemove = _anonymizedNames.Keys
                    .Where(name => !activeNames.Contains(name))
                    .ToList();
                
                foreach (var name in namesToRemove)
                {
                    _anonymizedNames.Remove(name);
                }
            }
        }
        catch (Exception ex)
        {
            Framework.Service<IPluginLog>().Error(ex, "Error in MarePlayerMarker.OnTick");
            RemoveAllMarkers();
        }
    }
} 