using System.Collections.Generic;
using Umbra.Common;

namespace Umbra.MarePlayerMarker.Localization;

public static class LocalizationManager
{
    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new();
    private static string _currentLanguage = "zh";
    
    static LocalizationManager()
    {
        LoadTranslations();
        SetLanguageFromUmbra();
    }
    
    private static void SetLanguageFromUmbra()
    {
        try
        {
            var umbraLang = I18N.GetCurrentLanguage();
            _currentLanguage = umbraLang == "zh" ? "zh" : "en"; 
        }
        catch
        {
            _currentLanguage = "en";
        }
    }
    
    private static void LoadTranslations()
    {
        // 加载内置翻译
        var zhTranslations = new Dictionary<string, string>
        {
            ["AutoClear.On"] = "自动清理：开",
            ["AutoClear.Off"] = "自动清理：关",
            ["AutoClear.Label"] = "自动清理",
            ["Anonymize.On"] = "角色匿名：开", 
            ["Anonymize.Off"] = "角色匿名：关",
            ["Anonymize.Label"] = "角色匿名",
            ["VfxMarker.On"] = "特效标记：开",
            ["VfxMarker.Off"] = "特效标记：关",
            ["VfxMarker.Label"] = "特效标记",
            ["Status.On"] = "开",
            ["Status.Off"] = "关",
            ["NoSyncPlayers"] = "暂无同步玩家",
            ["Distance.Meters"] = "米",
            ["Distance.NotVisible"] = "不可见",
            ["ComponentSettings"] = "组件设置",
            ["SyncPlayers"] = "同步玩家",
            ["Config.HideIfEmpty.Name"] = "如果没有同步玩家，则隐藏组件。",
            ["Config.HideIfEmpty.Description"] = "如果当前没有通过Mare同步的玩家，隐藏组件。",
            ["Config.UseUnicodeIcon.Name"] = "使用Unicode图标",
            ["Config.UseUnicodeIcon.Description"] = "使用Unicode字符\uE044作为图标",
            ["Config.UpdateInterval.Name"] = "更新间隔 (秒)",
            ["Config.UpdateInterval.Description"] = "组件刷新间隔，支持小数，最小0.05。",
            ["Config.AutoClearInvisible.Name"] = "自动清理不可见玩家",
            ["Config.AutoClearInvisible.Description"] = "开启后，列表会自动移除所有不可见玩家。",
            ["Widget.Name"] = "Mare同步玩家组件",
            ["Widget.Description"] = "提供一个清单显示通过Mare与你同步的玩家。",
            
            // MarePlayerMarker 相关翻译
            ["Marker.Name"] = "Mare同步玩家标记",
            ["Marker.Description"] = "显示通过Mare与你同步的玩家的世界标记。",
            ["Marker.Config.ShowName.Name"] = "显示名称",
            ["Marker.Config.ShowName.Description"] = "在世界标记上显示同步玩家的名字。",
            ["Marker.Config.AnonymizeName.Name"] = "匿名",
            ["Marker.Config.AnonymizeName.Description"] = "为角色名称打码。",
            ["Marker.Config.ShowHomeWorld.Name"] = "显示服务器",
            ["Marker.Config.ShowHomeWorld.Description"] = "在世界标记上显示玩家的服务器名称。",
            ["Marker.Config.UseUnicodeIcon.Name"] = "使用Unicode图标",
            ["Marker.Config.UseUnicodeIcon.Description"] = "使用Unicode字符\uE044作为图标，而不是使用图标ID。",
            ["Marker.Config.ShowCompassText.Name"] = "在罗盘上显示文本",
            ["Marker.Config.ShowCompassText.Description"] = "在罗盘上显示Unicode图标和玩家名称，而不仅仅是图标。",
            ["Marker.Config.VfxId.Name"] = "特效（开着卸载插件可能炸游戏）",
            ["Marker.Config.VfxId.Description"] = "显示在同步玩家身上的视觉效果。（非循环特效会炸游戏，虽然我挑选过了，但说不定验证漏了呢）",
            ["Marker.Config.IconId.Name"] = "同步玩家图标 ID",
            ["Marker.Config.IconId.Description"] = "用于世界标记的图标 ID。使用值0可禁用图标。在聊天中输入\"/xldata icons\"以访问图标浏览器。",
            ["Marker.Config.MarkerHeight.Name"] = "标记相对于目标的高度",
            ["Marker.Config.MarkerHeight.Description"] = "指定世界标记相对于同步玩家位置的高度。值为0时，标记将放置在目标的脚下。",
            ["Marker.Config.FadeDistance.Name"] = "消失距离",
            ["Marker.Config.FadeDistance.Description"] = "标记开始消失的距离。",
            ["Marker.Config.FadeAttenuation.Name"] = "渐隐距离",
            ["Marker.Config.FadeAttenuation.Description"] = "标记从开始消失到完全消失的距离。",
            ["Marker.Config.MaxVisibleDistance.Name"] = "最大可见距离",
            ["Marker.Config.MaxVisibleDistance.Description"] = "标记的最大可见距离。设置为0表示无限制。",
            
            // VFX 选项翻译
            ["Vfx.None"] = "无",
            ["Vfx.Basic.Light"] = "【基础】光",
            ["Vfx.Basic.Levitate"] = "【基础】悬浮",
            ["Vfx.Basic.Admirer"] = "【基础】爱慕者",
            ["Vfx.Surround.Mark1"] = "【环绕】环绕标记1",
            ["Vfx.Surround.Mark2"] = "【环绕】环绕标记2",
            ["Vfx.Surround.Mark3"] = "【环绕】环绕标记3",
            ["Vfx.Surround.Large"] = "【环绕】大型环绕",
            ["Vfx.Surround.RotatingSphere"] = "【环绕】旋转球",
            ["Vfx.Tsukuyomi.White3"] = "【月读】白3层",
            ["Vfx.Tsukuyomi.White6"] = "【月读】白6层",
            ["Vfx.Tsukuyomi.White10"] = "【月读】白10层",
            ["Vfx.Tsukuyomi.Black3"] = "【月读】黑3层",
            ["Vfx.Tsukuyomi.Black6"] = "【月读】黑6层",
            ["Vfx.Tsukuyomi.Black10"] = "【月读】黑10层",
            ["Vfx.Special.PrismaticCage"] = "【特殊】棱形牢笼",
            ["Vfx.Special.Hands"] = "【特殊】地下冒出很多手",
            ["Vfx.Special.BlueAura"] = "【特殊】蓝色光晕",
            ["Vfx.Special.RedVortex"] = "【特殊】红色漩涡",
            ["Vfx.Mark.HolyBombardment"] = "【标记】圣光轰炸"
        };
        
        var enTranslations = new Dictionary<string, string>
        {
            ["AutoClear.On"] = "Auto Clear: On",
            ["AutoClear.Off"] = "Auto Clear: Off",
            ["AutoClear.Label"] = "Auto Clear",
            ["Anonymize.On"] = "Anonymize: On",
            ["Anonymize.Off"] = "Anonymize: Off",
            ["Anonymize.Label"] = "Anonymize",
            ["VfxMarker.On"] = "VFX Marker: On",
            ["VfxMarker.Off"] = "VFX Marker: Off",
            ["VfxMarker.Label"] = "VFX Marker",
            ["Status.On"] = "On",
            ["Status.Off"] = "Off",
            ["NoSyncPlayers"] = "No synced players",
            ["Distance.Meters"] = "m",
            ["Distance.NotVisible"] = "Not visible",
            ["ComponentSettings"] = "Widget Settings", 
            ["SyncPlayers"] = "Synced Players",
            ["Config.HideIfEmpty.Name"] = "Hide widget if no synced players",
            ["Config.HideIfEmpty.Description"] = "Hide the widget when there are no players synced through Mare.",
            ["Config.UseUnicodeIcon.Name"] = "Use Unicode Icon",
            ["Config.UseUnicodeIcon.Description"] = "Use Unicode character \uE044 as icon.",
            ["Config.UpdateInterval.Name"] = "Update Interval (seconds)",
            ["Config.UpdateInterval.Description"] = "Widget refresh interval, supports decimals, minimum 0.05.",
            ["Config.AutoClearInvisible.Name"] = "Auto clear invisible players",
            ["Config.AutoClearInvisible.Description"] = "When enabled, the list will automatically remove all invisible players.",
            ["Widget.Name"] = "Mare Synced Players Widget",
            ["Widget.Description"] = "Provides a list showing players synced with you through Mare.",
            
            // MarePlayerMarker 相关翻译
            ["Marker.Name"] = "Mare Synced Players Marker",
            ["Marker.Description"] = "Shows world markers for players synced with you through Mare.",
            ["Marker.Config.ShowName.Name"] = "Show Name",
            ["Marker.Config.ShowName.Description"] = "Display the synced player's name on the world marker.",
            ["Marker.Config.AnonymizeName.Name"] = "Anonymize",
            ["Marker.Config.AnonymizeName.Description"] = "Anonymize character names.",
            ["Marker.Config.ShowHomeWorld.Name"] = "Show Home World",
            ["Marker.Config.ShowHomeWorld.Description"] = "Display the player's home world name on the world marker.",
            ["Marker.Config.UseUnicodeIcon.Name"] = "Use Unicode Icon",
            ["Marker.Config.UseUnicodeIcon.Description"] = "Use Unicode character \uE044 as icon instead of using icon ID.",
            ["Marker.Config.ShowCompassText.Name"] = "Show Text on Compass",
            ["Marker.Config.ShowCompassText.Description"] = "Show Unicode icon and player name on compass instead of just icon.",
            ["Marker.Config.VfxId.Name"] = "VFX (May crash if unloaded improperly)",
            ["Marker.Config.VfxId.Description"] = "Visual effects displayed on synced players. (Non-looping effects may crash the game, though I've selected carefully, there might be validation gaps)",
            ["Marker.Config.IconId.Name"] = "Synced Players Icon ID",
            ["Marker.Config.IconId.Description"] = "Icon ID for world markers. Use value 0 to disable icon. Type \"/xldata icons\" in chat to access icon browser.",
            ["Marker.Config.MarkerHeight.Name"] = "Marker Height Relative to Target",
            ["Marker.Config.MarkerHeight.Description"] = "Specifies the height of world markers relative to synced player position. Value 0 places marker at target's feet.",
            ["Marker.Config.FadeDistance.Name"] = "Fade Distance",
            ["Marker.Config.FadeDistance.Description"] = "Distance at which markers start to fade.",
            ["Marker.Config.FadeAttenuation.Name"] = "Fade Attenuation",
            ["Marker.Config.FadeAttenuation.Description"] = "Distance from start fading to completely invisible.",
            ["Marker.Config.MaxVisibleDistance.Name"] = "Maximum Visible Distance",
            ["Marker.Config.MaxVisibleDistance.Description"] = "Maximum visible distance for markers. Set to 0 for unlimited.",
            
            // VFX 选项翻译
            ["Vfx.None"] = "None",
            ["Vfx.Basic.Light"] = "[Basic] Light",
            ["Vfx.Basic.Levitate"] = "[Basic] Levitate",
            ["Vfx.Basic.Admirer"] = "[Basic] Admirer",
            ["Vfx.Surround.Mark1"] = "[Surround] Mark 1",
            ["Vfx.Surround.Mark2"] = "[Surround] Mark 2",
            ["Vfx.Surround.Mark3"] = "[Surround] Mark 3",
            ["Vfx.Surround.Large"] = "[Surround] Large Ring",
            ["Vfx.Surround.RotatingSphere"] = "[Surround] Rotating Sphere",
            ["Vfx.Tsukuyomi.White3"] = "[Tsukuyomi] White 3-Stack",
            ["Vfx.Tsukuyomi.White6"] = "[Tsukuyomi] White 6-Stack",
            ["Vfx.Tsukuyomi.White10"] = "[Tsukuyomi] White 10-Stack",
            ["Vfx.Tsukuyomi.Black3"] = "[Tsukuyomi] Black 3-Stack",
            ["Vfx.Tsukuyomi.Black6"] = "[Tsukuyomi] Black 6-Stack",
            ["Vfx.Tsukuyomi.Black10"] = "[Tsukuyomi] Black 10-Stack",
            ["Vfx.Special.PrismaticCage"] = "[Special] Prismatic Cage",
            ["Vfx.Special.Hands"] = "[Special] Underground Hands",
            ["Vfx.Special.BlueAura"] = "[Special] Blue Aura",
            ["Vfx.Special.RedVortex"] = "[Special] Red Vortex",
            ["Vfx.Mark.HolyBombardment"] = "[Mark] Holy Bombardment"
        };
        
        Translations["zh"] = zhTranslations;
        Translations["en"] = enTranslations;
    }
    
    public static string GetText(string key)
    {
        if (Translations.TryGetValue(_currentLanguage, out var langDict) && 
            langDict.TryGetValue(key, out var translation))
        {
            return translation;
        }
        
        if (Translations.TryGetValue("en", out var enDict) && 
            enDict.TryGetValue(key, out var enTranslation))
        {
            return enTranslation;
        }
        
        return $"[{key}]";
    }
    
    public static void SetLanguage(string language)
    {
        if (Translations.ContainsKey(language))
        {
            _currentLanguage = language;
        }
    }
} 