using System.Collections.Generic;

namespace Elypha.I18N
{
    public class MaterialBulkSwapperI18N : TemplateI18N
    {
        public MaterialBulkSwapperI18N(PluginLanguage language) : base(language)
        {
            MergeCustomLocalisation(customLocalisation);
        }

        private readonly Dictionary<string, Dictionary<PluginLanguage, string>> customLocalisation = new()
        {
            { "Path relative to", new() {
                { PluginLanguage.ChineseSimplified, "路径相对于" },
                { PluginLanguage.Japanese, "パス相対" },
            }},
            { "Outfit Object", new() {
                { PluginLanguage.ChineseSimplified, "衣服对象" },
                { PluginLanguage.Japanese, "衣装オブジェクト" },
            }},
            { "Reload", new() {
                { PluginLanguage.ChineseSimplified, "重新加载" },
                { PluginLanguage.Japanese, "リロード" },
            }},
            { "Edit", new() {
                { PluginLanguage.ChineseSimplified, "编辑" },
                { PluginLanguage.Japanese, "編集" },
            }},
            { "Select All", new() {
                { PluginLanguage.ChineseSimplified, "全选" },
                { PluginLanguage.Japanese, "すべて選択" },
            }},
            { "Select None", new() {
                { PluginLanguage.ChineseSimplified, "全不选" },
                { PluginLanguage.Japanese, "選択解除" },
            }},
            { "Unique Materials", new() {
                { PluginLanguage.ChineseSimplified, "独特材质" },
                { PluginLanguage.Japanese, "ユニークマテリアル" },
            }},
            { "Select Action", new() {
                { PluginLanguage.ChineseSimplified, "选择操作" },
                { PluginLanguage.Japanese, "操作" },
            }},
            { "Replace In-Place", new() {
                { PluginLanguage.ChineseSimplified, "就地替换" },
                { PluginLanguage.Japanese, "直接に置き換え" },
            }},
            { "Replace In Animation", new() {
                { PluginLanguage.ChineseSimplified, "在动画中替换" },
                { PluginLanguage.Japanese, "アニメーションで置き換え" },
            }},
            { "Select Animation Clip for swap", new() {
                { PluginLanguage.ChineseSimplified, "选择替换动画" },
                { PluginLanguage.Japanese, "置き換えアニメーションを選択" },
            }},
            { "Replace Material", new() {
                { PluginLanguage.ChineseSimplified, "替换材质" },
                { PluginLanguage.Japanese, "マテリアルを置き換え" },
            }},
            { "Restore", new() {
                { PluginLanguage.ChineseSimplified, "恢复" },
                { PluginLanguage.Japanese, "復元" },
            }},
            { "Animation Clip", new() {
                { PluginLanguage.ChineseSimplified, "动画" },
                { PluginLanguage.Japanese, "アニメーションクリップ" },
            }},
            { "Target Material", new() {
                { PluginLanguage.ChineseSimplified, "目标材质" },
                { PluginLanguage.Japanese, "目標マテリアル" },
            }},
            { "Target Frame", new() {
                { PluginLanguage.ChineseSimplified, "目标帧" },
                { PluginLanguage.Japanese, "目標フレーム" },
            }},
            { "Add keyframe", new() {
                { PluginLanguage.ChineseSimplified, "添加关键帧" },
                { PluginLanguage.Japanese, "キーフレームを追加" },
            }},
        };
    }
}