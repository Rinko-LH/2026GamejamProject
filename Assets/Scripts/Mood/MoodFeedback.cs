using RoomDemo.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoomDemo.Mood
{
    /// <summary>
    /// 低落表现（S8，策划案 5.4）：任一状态低于阈值时，淡入 URP 全局后处理，
    /// 通过 Volume 权重降低饱和度并增加暗角；低落解除或重开时淡出。
    /// 玩家头侧的占位气泡仍由本组件显示。移速变化由移动脚本读取
    /// Services.Stats.IsLowMood 处理。
    /// </summary>
    public class MoodFeedback : MonoBehaviour, IResettable
    {
        [Header("引用（装配注入）")]
        [Tooltip("配置了 Color Adjustments 与 Vignette 的全局 Volume")]
        public Volume moodVolume;

        [Tooltip("气泡挂点（Player 的 Transform，子物体自动跟随）")]
        public Transform bubbleAnchor;

        [Header("参数")]
        [Tooltip("后处理淡入/淡出时长（秒）")]
        public float fadeSeconds = 1f;

        [Range(0f, 1f)]
        [Tooltip("低落时后处理 Volume 的最大权重")]
        public float maxVolumeWeight = 1f;

        [Tooltip("气泡文案（占位）")]
        public string bubbleText = "好累……";

        /// <summary>气泡压在所有场景精灵（150xx）之上、UI 之下。</summary>
        const int OrderBubble = 15110;

        GameObject _bubble;
        float _volumeWeight;
        bool _low;
        bool _built;
        PlayerStats _subscribedStats;

        void Start() => Build();

        void Build()
        {
            if (_built) return;
            _built = true;

            // Volume 的初始权重由场景设为 0，低落状态到来后再淡入。
            if (moodVolume != null)
            {
                _volumeWeight = moodVolume.weight;
            }

            if (bubbleAnchor == null) return;

            var old = bubbleAnchor.Find("MoodBubble");
            if (old != null) Object.Destroy(old.gameObject); // 防装配/重跑残留双份

            var go = new GameObject("MoodBubble");
            go.transform.SetParent(bubbleAnchor, false);
            go.transform.localPosition = new Vector3(0.9f, 0.75f, 0f);
            go.SetActive(false);

            var sr = go.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var white = new Color32(255, 255, 255, 255);
            tex.SetPixels32(new[] { white, white, white, white });
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
            sr.color = new Color(1f, 0.97f, 0.88f); // 淡黄便签色
            sr.sortingOrder = OrderBubble;
            go.transform.localScale = new Vector3(1.3f, 0.62f, 1f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = bubbleText;
            tm.font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 48);
            tm.fontSize = 48;
            tm.characterSize = 0.065f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(0.30f, 0.28f, 0.32f);
            var mr = textGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = OrderBubble + 1;

            _bubble = go;
        }

        void Update()
        {
            Build();
            SubscribeStats();
            UpdatePostProcessing();
        }

        void UpdatePostProcessing()
        {
            if (moodVolume == null) return;

            float target = _low ? maxVolumeWeight : 0f;
            float duration = Mathf.Max(0.01f, fadeSeconds);
            _volumeWeight = Mathf.MoveTowards(_volumeWeight, target,
                (Mathf.Max(_volumeWeight, Mathf.Max(0f, maxVolumeWeight)) / duration) * Time.deltaTime);
            moodVolume.weight = _volumeWeight;
        }

        void SubscribeStats()
        {
            if (_subscribedStats != null) return;
            var stats = Services.Stats as PlayerStats;
            if (stats == null) return;
            stats.LowMoodChanged -= OnLowMoodChanged;
            stats.LowMoodChanged += OnLowMoodChanged;
            _subscribedStats = stats;

            // 订阅成功后同步一次当前态（错过边缘也不怕开局已低落）
            ApplyLow(stats.IsLowMood);
        }

        void OnLowMoodChanged(bool low) => ApplyLow(low);

        void ApplyLow(bool low)
        {
            _low = low;
            if (_bubble != null && _bubble.activeSelf != low)
                _bubble.SetActive(low);
        }

        void OnDestroy()
        {
            if (_subscribedStats != null)
            {
                _subscribedStats.LowMoodChanged -= OnLowMoodChanged;
                _subscribedStats = null;
            }
        }

        /// <summary>R 重开：后处理淡出归零、气泡收起。</summary>
        public void ResetForNewRun()
        {
            _low = false;
            _volumeWeight = 0f;
            if (moodVolume != null) moodVolume.weight = 0f;
            if (_bubble != null) _bubble.SetActive(false);
        }
    }
}