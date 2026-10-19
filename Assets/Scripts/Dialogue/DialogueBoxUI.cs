using RoomDemo.Contracts;
using TMPro;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 对话框（S1，策划案 1.6，取代原 IMGUI 占位）：订阅 Services.Dialogue.LineChanged，
    /// 屏幕底部居中画半透明黑底框：说话人名（独白时不显示）+ 台词 + 推进提示。
    ///
    /// 无打字机效果（E4：整行直接显示）。
    /// Box / Speaker / Body 在场景层级中制作；脚本只更新文本和控制对白框显隐。
    /// </summary>
    public class DialogueBoxUI : MonoBehaviour
    {
        GameObject _box;
        TMP_Text _speaker;
        TMP_Text _body;
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

            var box = transform.Find("Box");
            _box = box != null ? box.gameObject : null;
            if (_box == null)
            {
                Debug.LogError("[对白框] 缺少场景 UI 子物体 Box。", this);
                return;
            }
            _speaker = box.Find("Speaker")?.GetComponent<TMP_Text>();
            _body = box.Find("Body")?.GetComponent<TMP_Text>();
            if (_speaker == null || _body == null)
                Debug.LogError("[对白框] Box 下需要 Speaker 和 Body 两个 TextMeshProUGUI 子物体。", this);

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
            if (_speaker == null || _body == null) return;
            if (_box != null) _box.SetActive(true);
            if (_speaker != null) _speaker.text = line.speaker;
            if (_body != null) _body.text = line.text + "\n（空格/回车 推进 · Esc 跳过）";
        }
    }
}
