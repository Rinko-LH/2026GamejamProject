using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 对话框占位 UI（IMGUI，E1 决策）。订阅 DialogueManager.LineChanged，
    /// 在屏幕底部画半透明黑底框：说话人名（独白时不显示）+ 台词 + 推进提示。
    ///
    /// 无打字机效果（E4：整行直接显示）。
    /// 正式 uGUI 对话框（第 3 步 1.6 UI 四件套）做好后，本脚本连物体一起删。
    /// </summary>
    public class DialogueBoxUI : MonoBehaviour
    {
        [Header("布局")]
        public float boxWidth = 640f;
        public float boxHeight = 120f;
        public float bottomMargin = 40f;
        public int fontSize = 20;

        bool _visible;
        string _speaker = "";
        string _text = "";
        bool _subscribed;

        static GUIStyle _nameStyle;
        static GUIStyle _textStyle;
        static GUIStyle _hintStyle;

        void Update()
        {
            // 惰性订阅（全项目统一模式）：Awake 时 DialogueManager.Instance 可能还没就绪
            if (_subscribed) return;
            var d = DialogueManager.Instance;
            if (d == null) return;
            d.LineChanged -= OnLineChanged;
            d.LineChanged += OnLineChanged;
            _subscribed = true;
        }

        void OnDestroy()
        {
            var d = DialogueManager.Instance;
            if (d != null) d.LineChanged -= OnLineChanged;
            _subscribed = false;
        }

        void OnLineChanged(DialogueLine line)
        {
            if (line == null)
            {
                _visible = false;
                return;
            }
            _visible = true;
            _speaker = line.speaker;
            _text = line.text;
        }

        void OnGUI()
        {
            if (!_visible) return;

            if (_nameStyle == null)
                _nameStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft };
            if (_textStyle == null)
                _textStyle = new GUIStyle(GUI.skin.label)
                { fontSize = fontSize, alignment = TextAnchor.UpperLeft, wordWrap = true };
            if (_hintStyle == null)
                _hintStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 12, alignment = TextAnchor.LowerRight };

            float x = (Screen.width - boxWidth) * 0.5f;
            float y = Screen.height - bottomMargin - boxHeight;
            var box = new Rect(x, y, boxWidth, boxHeight);

            // 半透明黑底，和调试 HUD 一个路子，保证手绘背景上看得清
            var gc = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = gc;

            const float pad = 14f;
            bool hasName = !string.IsNullOrEmpty(_speaker);

            if (hasName)
                GUI.Label(new Rect(box.x + pad, box.y + 6f, box.width - pad * 2f, 22f), _speaker, _nameStyle);

            // 台词正文：整行直接显示（无打字机）
            float textTop = hasName ? 32f : 16f;
            GUI.Label(new Rect(box.x + pad, box.y + textTop, box.width - pad * 2f, box.height - textTop - 26f),
                _text, _textStyle);

            GUI.Label(new Rect(box.x + pad, box.yMax - 26f, box.width - pad * 2f, 20f),
                "空格/回车 推进 · Esc 跳过", _hintStyle);
        }
    }
}
