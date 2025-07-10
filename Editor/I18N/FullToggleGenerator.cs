using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;

namespace Elypha.I18N
{
    public class FullToggleGeneratorI18N
    {
        public PluginLanguage language;


        public FullToggleGeneratorI18N(PluginLanguage language)
        {
            this.language = language;
        }

        public string Localise(string key)
        {
            if (language == PluginLanguage.English) return key;

            Localisation.TryGetValue(key, out var data);
            if (data is null) return key;

            data.TryGetValue(language, out var text);
            return text ?? key;
        }

        private static readonly Dictionary<string, Dictionary<PluginLanguage, string>> Localisation = new()
        {
            { "Settings", new() {
                { PluginLanguage.ChineseSimplified, "设置" },
                { PluginLanguage.Japanese, "設定" },
            }},
            { "Object Groups", new() {
                { PluginLanguage.ChineseSimplified, "对象组" },
                { PluginLanguage.Japanese, "オブジェクトグループ" },
            }},
            { "Target Animation Clip", new() {
                { PluginLanguage.ChineseSimplified, "目标动画" },
                { PluginLanguage.Japanese, "目標アニメーションクリップ" },
            }},
            { "Select Action", new() {
                { PluginLanguage.ChineseSimplified, "选择操作" },
                { PluginLanguage.Japanese, "操作" },
            }},
            { "Generate Full Toggle Animation", new() {
                { PluginLanguage.ChineseSimplified, "生成全切换动画" },
                { PluginLanguage.Japanese, "フルトグルアニメーションを生成" },
            }},
        };
    }
}
