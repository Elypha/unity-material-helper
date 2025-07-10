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
            Localisation.TryGetValue(key, out var data);
            if (data is null) return key;
            data.TryGetValue(language, out var text);
            return text ?? key;
        }

        private static readonly Dictionary<string, Dictionary<PluginLanguage, string>> Localisation = new()
        {
            { "Replace In-Place", new() {
                { PluginLanguage.English, "Replace In-Place" },
                { PluginLanguage.Japanese, "置換" },
            }},
        };
    }
}
