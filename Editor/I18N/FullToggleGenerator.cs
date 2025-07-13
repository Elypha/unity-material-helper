using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;

namespace Elypha.I18N
{
    public class FullToggleGeneratorI18N : TemplateI18N
    {
        public FullToggleGeneratorI18N(PluginLanguage language) : base(language)
        {
            MergeCustomLocalisation(customLocalisation);
        }

        private readonly Dictionary<string, Dictionary<PluginLanguage, string>> customLocalisation = new()
        {
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
