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

    public override string Id          => "Umbra_MarePlayerMarker";
    public override string Name        => "Mare同步玩家标记";
    public override string Description => "显示通过Mare与你同步的玩家的世界标记。";

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
                "ShowUid",
                "显示UID",
                "在世界标记上显示玩家的游戏UID。",
                false
            ),
            new SelectMarkerConfigVariable(
                "VfxId",
                "效果",
                "显示在同步玩家身上的视觉效果。",
                "无",
                new() {
                    { "", "无" },
                    { "vfx/common/eff/cmrz_castx1c.avfx", "光" },
                    { "vfx/common/eff/levitate0f.avfx", "悬浮" },
                    { "vfx/common/eff/m0328sp10st0f.avfx", "旋转球" },
                    { "vfx/common/eff/dkst_over_p0f.avfx", "蓝色光环" },
                    { "vfx/common/eff/st_akama_kega0j.avfx", "红色漩涡" },
                    { "vfx/common/eff/dk10ht_cha0h.avfx", "爱慕者" }
                }
            ),
            new IntegerMarkerConfigVariable(
                "IconId",
                "同步玩家图标 ID",
                "用于世界标记的图标 ID。使用值0可禁用图标。在聊天中输入\"/xldata icons\"以访问图标浏览器。",
                60401
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
            var     showUid          = GetConfigValue<bool>("ShowUid");
            var     showOnCompass    = GetConfigValue<bool>("ShowOnCompass");
            var     fadeDistance     = GetConfigValue<int>("FadeDistance");
            var     fadeAttenuation  = GetConfigValue<int>("FadeAttenuation");
            var     maxVisibleDistance = GetConfigValue<int>("MaxVisibleDistance");
            Vector2 fadeDist         = new(fadeDistance, fadeDistance + Math.Max(1, fadeAttenuation));

            // 按Y坐标排序，确保标记从上到下排列
            targets.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));

            // 计算每个玩家的标记位置
            var markerPositions = new Dictionary<ulong, Vector3>();
            foreach (var obj in targets) {
                var basePosition = obj.Position with { Y = obj.Position.Y + markerHeight };
                
                // 检查是否有其他标记在附近
                var nearbyMarkers = markerPositions.Values
                    .Where(p => Vector3.Distance(p, basePosition) < 2.0f)
                    .OrderBy(p => p.Y)
                    .ToList();

                if (nearbyMarkers.Count > 0) {
                    // 如果有其他标记，将新标记放在最高的标记上方
                    basePosition = basePosition with { Y = nearbyMarkers.Last().Y + 2.0f };
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

                SetMarker(
                    new() {
                        Key           = key,
                        MapId         = zoneId,
                        IconId        = iconId,
                        Position      = markerPositions[obj.GameObjectId],
                        Label         = label,
                        FadeDistance  = fadeDist,
                        ShowOnCompass = showOnCompass,
                    }
                );
            }

            RemoveMarkersExcept(usedIds);
        }
        catch (Exception ex)
        {
            Framework.Service<IPluginLog>().Error(ex, "Error in MarePlayerMarker.OnTick");
            RemoveAllMarkers();
        }
    }
} 