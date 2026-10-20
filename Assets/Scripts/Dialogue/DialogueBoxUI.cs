using RoomDemo.Contracts;
using TMPro;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>逐字显示对白；完整显示后以浮动三角提示玩家推进。</summary>
    public class DialogueBoxUI : MonoBehaviour
    {
        [Header("对白显示")]
        [Min(1f)] public float charactersPerSecond = 30f;
        // 旧场景缺少三角时的兼容默认值；场景三角的外观由自身组件决定。
        [SerializeField, HideInInspector] Vector2 indicatorSize = new Vector2(18f, 12f);
        [SerializeField, HideInInspector] Vector2 indicatorPosition = new Vector2(-24f, 18f);
        [SerializeField, HideInInspector] Color indicatorColor = Color.white;
        [Header("推进三角浮动")]
        [Min(0f)] public float indicatorFloatAmplitude = 3f;
        [Min(0f)] public float indicatorFloatFrequency = 1.2f;

        GameObject _box;
        TMP_Text _body;
        RectTransform _indicator;
        Vector2 _indicatorBasePosition;
        IDialogueService _dialogue;
        bool _subscribed;
        int _characterCount;
        float _revealProgress;
        float _indicatorTime;
        bool _revealing;
        DialogueLine _displayedLine;

        void Start()
        {
            Build();
            Subscribe();
        }

        void Build()
        {
            if (_box != null) return;
            var box = transform.Find("Box");
            if (box == null)
            {
                Debug.LogError("[对白框] 缺少场景 UI 子物体 Box。", this);
                return;
            }
            _box = box.gameObject;
            _body = box.Find("Body")?.GetComponent<TMP_Text>();
            if (_body == null)
                Debug.LogError("[对白框] Box 下需要 Body 文本组件（TextMeshProUGUI）。", this);

            // 由对白框拥有的装饰，不拦截全屏鼠标输入。
            var existing = box.Find("ContinueIndicator");
            if (existing != null) _indicator = existing.GetComponent<RectTransform>();
            if (_indicator == null)
            {
                var indicator = new GameObject("ContinueIndicator", typeof(RectTransform), typeof(RoundedDialogueTriangle));
                indicator.layer = gameObject.layer;
                _indicator = indicator.GetComponent<RectTransform>();
                _indicator.SetParent(box, false);
                _indicator.anchorMin = _indicator.anchorMax = new Vector2(1f, 0f);
                _indicator.pivot = new Vector2(0.5f, 0.5f);
                _indicator.sizeDelta = indicatorSize;
                _indicator.anchoredPosition = indicatorPosition;
                indicator.GetComponent<RoundedDialogueTriangle>().color = indicatorColor;
            }
            _indicatorBasePosition = _indicator.anchoredPosition;
            _indicator.gameObject.SetActive(false);
            _box.SetActive(false);
        }

        void Update()
        {
            Build();
            if (!_subscribed) Subscribe();
            if (_revealing && _body != null)
            {
                _revealProgress += Time.unscaledDeltaTime * Mathf.Max(1f, charactersPerSecond);
                _body.maxVisibleCharacters = Mathf.Min(_characterCount, Mathf.FloorToInt(_revealProgress));
                if (_body.maxVisibleCharacters >= _characterCount) CompleteReveal();
            }
            if (_indicator != null && _indicator.gameObject.activeSelf)
            {
                _indicatorTime += Time.unscaledDeltaTime;
                _indicator.anchoredPosition = _indicatorBasePosition + Vector2.up *
                    (Mathf.Sin(_indicatorTime * Mathf.PI * 2f * indicatorFloatFrequency) * indicatorFloatAmplitude);
            }
        }

        void Subscribe()
        {
            var d = Services.Dialogue;
            if (d == null) return;
            _dialogue = d;
            d.LineChanged += OnLineChanged;
            d.RevealRequested += CompleteReveal;
            _subscribed = true;
            d.ReplayCurrent();
        }

        void OnDestroy()
        {
            if (_dialogue == null) return;
            _dialogue.LineChanged -= OnLineChanged;
            _dialogue.RevealRequested -= CompleteReveal;
            _dialogue.SetRevealing(false);
        }

        void CompleteReveal()
        {
            if (_displayedLine == null || _body == null) return;
            _revealing = false;
            _body.maxVisibleCharacters = _characterCount;
            _dialogue?.SetRevealing(false);
            ShowIndicator(true);
        }

        void ShowIndicator(bool visible)
        {
            if (_indicator == null) return;
            _indicatorTime = 0f;
            _indicator.anchoredPosition = _indicatorBasePosition;
            _indicator.gameObject.SetActive(visible);
        }

        void OnLineChanged(DialogueLine line)
        {
            _displayedLine = line;
            _revealing = false;
            _dialogue?.SetRevealing(false);
            ShowIndicator(false);
            if (line == null)
            {
                if (_box != null) _box.SetActive(false);
                return;
            }
            if (_body == null) return;
            _box.SetActive(true);
            _body.text = line.text ?? string.Empty;
            _body.maxVisibleCharacters = int.MaxValue;
            _body.ForceMeshUpdate();
            // TMP 解析后的字符数，不把富文本标签作为打字字符。
            _characterCount = _body.textInfo.characterCount;
            _body.maxVisibleCharacters = 0;
            _revealProgress = 0f;
            _revealing = _characterCount > 0;
            _dialogue?.SetRevealing(_revealing);
            if (!_revealing) CompleteReveal();
            Debug.Log("[对话操作] 空格 / E / 鼠标左键（全屏）：补全本句，再次操作进入下一句；Esc 跳过", this);
        }
    }
}
