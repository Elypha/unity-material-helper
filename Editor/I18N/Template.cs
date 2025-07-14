using System.Collections.Generic;

namespace Elypha.I18N
{
    public class TemplateI18N
    {
        public PluginLanguage language;


        public TemplateI18N(PluginLanguage language)
        {
            SetLanguage(language);
        }

        public void SetLanguage(PluginLanguage newLanguage)
        {
            language = newLanguage;
        }

        public string Localise(string key)
        {
            if (language == PluginLanguage.English) return key;

            Localisation.TryGetValue(key, out var data);
            if (data is null) return key;

            data.TryGetValue(language, out var text);
            return text ?? key;
        }

        protected static void MergeCustomLocalisation(Dictionary<string, Dictionary<PluginLanguage, string>> customLocalisation)
        {
            foreach (var kvp in customLocalisation)
            {
                if (Localisation.ContainsKey(kvp.Key))
                {
                    Localisation[kvp.Key] = kvp.Value;
                }
                else
                {
                    Localisation.Add(kvp.Key, kvp.Value);
                }
            }
        }

        protected static readonly Dictionary<string, Dictionary<PluginLanguage, string>> Localisation = new()
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
