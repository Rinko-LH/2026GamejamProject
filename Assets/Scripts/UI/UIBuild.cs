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
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var t = rt.gameObject.AddComponent<Text>();
            // 不显式指定字体：AddComponent<Text>() 默认带项目默认字体（Arial 兼容），
            // 避免 Resources.GetBuiltinResource("Arial.ttf") 在 2022.3 抛异常。
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
