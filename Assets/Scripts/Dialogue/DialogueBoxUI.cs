using RoomDemo.Contracts;
using RoomDemo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo
{
    /// <summary>
    /// 对话框（S1，策划案 1.6，取代原 IMGUI 占位）：订阅 Services.Dialogue.LineChanged，
    /// 屏幕底部居中画半透明黑底框：说话人名（独白时不显示）+ 台词 + 推进提示。
    ///
    /// 无打字机效果（E4：整行直接显示）。
    /// Start 里自建房梁（背景图 + 两个文本），美术到位可换成带图版 prefab。
    /// </summary>
    public class DialogueBoxUI : MonoBehaviour
    {
        GameObject _box;
        Text _speaker;
        Text _body;
        bool _subscribed;

        void Start()
        {
            Build();
            // 订阅必须在这里就做，不能在 Update 里等到第一帧——
            // GameManager.Start 阶段就派发了开场第一行，所有 Start 先于所有 Update，
            // 等到第一帧再订阅时首句已经过去（P6：开场第一句整段消失）。
            Subscribe();
        }

        void Build()
        {
            if (_box != null) return;

            // 底部居中半透明黑底（接近屏宽 1728 / 高 440，离底 30）
            var img = UIBuild.Image(transform, "Box", new Color(0f, 0f, 0f, 0.78f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-864f, 30f), new Vector2(864f, 470f));
            _box = img.gameObject;

            _speaker = UIBuild.Text(_box.transform, "Speaker", 56, TextAnchor.UpperLeft,
                new Color(1f, 0.85f, 0.5f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(1680f, 70));
            _body = UIBuild.Text(_box.transform, "Body", 72, TextAnchor.UpperLeft, Color.white,
                new Vector2(0f, 1f), new Vector2(24f, -96f), new Vector2(1680f, 300));

            _box.SetActive(false);
        }

        void Update()
        {
            Build();
            if (!_subscribed) Subscribe();
        }

        /// <summary>幂等订阅。订阅成功后立刻补一次当前行，挽回迟到的首句（P6）。</summary>
        void Subscribe()
        {
            var d = Services.Dialogue;
            if (d == null) return;

            d.LineChanged -= OnLineChanged;
            d.LineChanged += OnLineChanged;
            _subscribed = true;

            d.ReplayCurrent();
        }

        void OnDestroy()
        {
            if (Services.Dialogue != null) Services.Dialogue.LineChanged -= OnLineChanged;
            _subscribed = false;
        }

        void OnLineChanged(DialogueLine line)
        {
            if (line == null)
            {
                if (_box != null) _box.SetActive(false);
                return;
            }
            if (_box != null) _box.SetActive(true);
            if (_speaker != null) _speaker.text = line.speaker;
            if (_body != null) _body.text = line.text + "\n（空格/回车 推进 · Esc 跳过）";
        }
    }
}
