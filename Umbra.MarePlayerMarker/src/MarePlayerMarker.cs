using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Umbra.Common;
using Umbra.Game;
using Umbra.Markers;
using Umbra.MarePlayerMarker.Localization;

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
    public override string Name        => LocalizationManager.GetText("Marker.Name");
    public override string Description => LocalizationManager.GetText("Marker.Description");

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
                LocalizationManager.GetText("Marker.Config.ShowName.Name"),
                LocalizationManager.GetText("Marker.Config.ShowName.Description"),
                true
            ),
            new BooleanMarkerConfigVariable(
                "AnonymizeName",
                LocalizationManager.GetText("Marker.Config.AnonymizeName.Name"),
                LocalizationManager.GetText("Marker.Config.AnonymizeName.Description"),
                false
            ),
            new BooleanMarkerConfigVariable(
                "ShowUid",
                LocalizationManager.GetText("Marker.Config.ShowUid.Name"),
                LocalizationManager.GetText("Marker.Config.ShowUid.Description"),
                false
            ),
            new BooleanMarkerConfigVariable(
                "UseUnicodeIcon",
                LocalizationManager.GetText("Marker.Config.UseUnicodeIcon.Name"),
                LocalizationManager.GetText("Marker.Config.UseUnicodeIcon.Description"),
                true
            ),
            new BooleanMarkerConfigVariable(
                "ShowCompassText",
                LocalizationManager.GetText("Marker.Config.ShowCompassText.Name"),
                LocalizationManager.GetText("Marker.Config.ShowCompassText.Description"),
                true
            ),
            new SelectMarkerConfigVariable(
                "VfxId",
                LocalizationManager.GetText("Marker.Config.VfxId.Name"),
                LocalizationManager.GetText("Marker.Config.VfxId.Description"),
                LocalizationManager.GetText("Vfx.None"),
                new() {
                    { "", LocalizationManager.GetText("Vfx.None") },
                    // 基础效果
                    { "vfx/common/eff/cmrz_castx1c.avfx", LocalizationManager.GetText("Vfx.Basic.Light") },
                    { "vfx/common/eff/levitate0f.avfx", LocalizationManager.GetText("Vfx.Basic.Levitate") },
                    { "vfx/common/eff/dk10ht_cha0h.avfx", LocalizationManager.GetText("Vfx.Basic.Admirer") },
                    
                    // 环绕效果
                    { "vfx/common/eff/dkst_evt01f.avfx", LocalizationManager.GetText("Vfx.Surround.Mark1") },
                    { "vfx/common/eff/x6fa_stlp01_c0a1.avfx", LocalizationManager.GetText("Vfx.Surround.Mark2") },
                    { "vfx/common/eff/x6fa_stlp02_c0a1.avfx", LocalizationManager.GetText("Vfx.Surround.Mark3") },
                    { "vfx/common/eff/m7105_stlp02_c0k1.avfx", LocalizationManager.GetText("Vfx.Surround.Large") },
                    { "vfx/common/eff/m0328sp10st0f.avfx", LocalizationManager.GetText("Vfx.Surround.RotatingSphere") },
                    
                    // 月读效果
                    { "vfx/common/eff/m0487_w3_mark0h.avfx", LocalizationManager.GetText("Vfx.Tsukuyomi.White3") },
                    { "vfx/common/eff/m0487_w6_mark0h.avfx", LocalizationManager.GetText("Vfx.Tsukuyomi.White6") },
                    { "vfx/common/eff/m0487_w10_mark0h.avfx", LocalizationManager.GetText("Vfx.Tsukuyomi.White10") },
                    { "vfx/common/eff/m0487_b3_mark0h.avfx", LocalizationManager.GetText("Vfx.Tsukuyomi.Black3") },
                    { "vfx/common/eff/m0487_b6_mark0h.avfx", LocalizationManager.GetText("Vfx.Tsukuyomi.Black6") },
                    { "vfx/common/eff/m0487_b10_mark0h.avfx", LocalizationManager.GetText("Vfx.Tsukuyomi.Black10") },
                    
                    // 特殊效果
                    { "vfx/common/eff/z6r1_b3_stlp05_c0t1.avfx", LocalizationManager.GetText("Vfx.Special.PrismaticCage") },
                    { "vfx/common/eff/dk10ht_sdb0c.avfx", LocalizationManager.GetText("Vfx.Special.Hands") },
                    { "vfx/common/eff/dkst_over_p0f.avfx", LocalizationManager.GetText("Vfx.Special.BlueAura") },
                    { "vfx/common/eff/st_akama_kega0j.avfx", LocalizationManager.GetText("Vfx.Special.RedVortex") },

                    // 标记
                    { "vfx/common/eff/n4g8_stlp_shlight1v.avfx", LocalizationManager.GetText("Vfx.Mark.HolyBombardment") },
                }
            ),
            new IntegerMarkerConfigVariable(
                "IconId",
                LocalizationManager.GetText("Marker.Config.IconId.Name"),
                LocalizationManager.GetText("Marker.Config.IconId.Description"),
                63936
            ),
            new IntegerMarkerConfigVariable(
                "MarkerHeight",
                LocalizationManager.GetText("Marker.Config.MarkerHeight.Name"),
                LocalizationManager.GetText("Marker.Config.MarkerHeight.Description"),
                2,
                -10,
                10
            ),
            new IntegerMarkerConfigVariable(
                "FadeDistance",
                LocalizationManager.GetText("Marker.Config.FadeDistance.Name"),
                LocalizationManager.GetText("Marker.Config.FadeDistance.Description"),
                10,
                0,
                100
            ),
            new IntegerMarkerConfigVariable(
                "FadeAttenuation",
                LocalizationManager.GetText("Marker.Config.FadeAttenuation.Name"),
                LocalizationManager.GetText("Marker.Config.FadeAttenuation.Description"),
                5,
                0,
                100
            ),
            new IntegerMarkerConfigVariable(
                "MaxVisibleDistance",
                LocalizationManager.GetText("Marker.Config.MaxVisibleDistance.Name"),
                LocalizationManager.GetText("Marker.Config.MaxVisibleDistance.Description"),
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