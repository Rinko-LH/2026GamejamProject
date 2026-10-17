using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo.UI
{
    /// <summary>
    /// uGUI 构建辅助：在占位期用系统字体快速搭出文本 / 背景，
    /// 让 S1 四件套的脚本能在 Start 里自建房梁 UI，无需手摆 prefab。
    /// 美术到位后这些脚本可整段替换为带图版，不动玩法代码。
    /// </summary>
    public static class UIBuild
    {
        /// <summary>
        /// 占位期默认字体。Unity 2022.3 移除了 Resources.GetBuiltinResource&lt;Font&gt;("Arial.ttf")，
        /// 而运行时 AddComponent&lt;Text&gt;() 的 font 默认是 null（文字不渲染）。
        /// 这里用操作系统字体兜底，避免依赖被移除的内置资源，也无需额外字体资产。
        /// 缓存为静态，所有文本共用一个 Font 实例。
        /// </summary>
        static Font _defaultFont;
        static Font GetDefaultFont()
        {
            if (_defaultFont != null) return _defaultFont;
            string[] names = { "Arial", "Segoe UI", "Microsoft YaHei", "Liberation Sans" };
            foreach (var n in names)
            {
                try
                {
                    var f = Font.CreateDynamicFontFromOSFont(n, 26);
                    if (f != null) { _defaultFont = f; break; }
                }
                catch (System.Exception) { /* 该字体名不可用，试下一个 */ }
            }
            return _defaultFont;
        }

        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        /// <summary>在 parent 下建一个锚定到指定角/边的文本。</summary>
        public static Text Text(Transform parent, string name, int fontSize, TextAnchor align, Color color,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            var rt = NewRect(parent, name);
            rt.anchorMin = rt.anchorMax = anchor;
            // 关键：pivot 必须跟锚点一致，offset 才是"距边缘的边距"。
            // 默认 pivot 是 (0.5,0.5) 居中，offset 定位的是矩形<b>中心</b>——
            // 于是 (20,-16) 会把 130 高的矩形上沿顶到角外 49px，而文本
            // 又是从矩形顶部开始排版，整行字就画到屏幕外去了（HUD 全不可见）。
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var t = rt.gameObject.AddComponent<Text>();
            t.font = GetDefaultFont();   // 关键：运行时必须显式给字体，否则文字不渲染
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.text = "";
            return t;
        }

        /// <summary>在 parent 下建一个拉伸填充的背景图（用锚边 + offset 定位）。</summary>
        public static Image Image(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = NewRect(parent, name);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }
    }
}
