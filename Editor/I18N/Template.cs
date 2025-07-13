using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;

namespace Elypha.I18N
{
    public class TemplateI18N
    {
        public PluginLanguage language;


        public TemplateI18N(PluginLanguage language)
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
        };
    }
}
