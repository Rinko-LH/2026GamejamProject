using RoomDemo.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoomDemo.Mood
{
    /// <summary>
    /// 低落表现（S8，策划案 5.4）：任一状态低于阈值时，淡入 URP 全局后处理，
    /// 通过 Volume 权重增加暗角，保留原有画面饱和度；低落解除或重开时淡出。
    /// 移速变化由移动脚本读取 Services.Stats.IsLowMood 处理。
    /// 玩家头侧的气泡在场景中摆好，低落时显示，解除时由 MoodBubbleMotion 飘散淡出。
    /// </summary>
    public class MoodFeedback : MonoBehaviour, IResettable
    {
        [Header("引用（装配注入）")]
        [Tooltip("只配置 Vignette 暗角的全局 Volume")]
        public Volume moodVolume;

        [InspectorName("口渴气泡")]
        [Tooltip("水分不足时显示的气泡；以场景中摆好的位置播放呼吸游移动画")]
        public GameObject bubble;

        [InspectorName("饥饿气泡"), Tooltip("饱食度不足时显示的气泡，保留场景中的位置")]
        public GameObject hungryBubble;

        [InspectorName("疲累气泡"), Tooltip("精神不足时显示的气泡，保留场景中的位置")]
        public GameObject tiredBubble;

        [Tooltip("气泡里的文字；留空时自动从气泡子物体中查找（包括隐藏物体）")]
        public TMP_Text bubbleText;

        [Header("状态提示文案")]
        [Tooltip("水分低于自身阈值时显示的文字")]
        public string thirstyText = "好渴……";
        [Tooltip("饱食度低于自身阈值时显示的文字")]
        public string hungryText = "好饿……";
        [Tooltip("精神低于自身阈值时显示的文字")]
        public string tiredText = "好累……";

        [Header("参数")]
        [Tooltip("后处理淡入/淡出时长（秒）")]
        public float fadeSeconds = 1f;

        [Range(0f, 1f)]
        [Tooltip("低落时后处理 Volume 的最大权重")]
        public float maxVolumeWeight = 1f;

        float _volumeWeight;
        bool _low;
        bool _built;
        PlayerStats _subscribedStats;
        GameObject[] _bubbles;
        MoodBubbleMotion[] _motions;
        TMP_Text[] _bubbleTexts;

        public int VisibleBubbleCount
        {
            get
            {
                int count = 0;
                if (_bubbles != null)
                    foreach (var item in _bubbles)
                        if (item != null && item.activeSelf) count++;
                return count;
            }
        }

        void Start() => Build();

        void Build()
        {
            if (_built) return;
            _built = true;
            if (bubbleText == null && bubble != null)
                bubbleText = bubble.GetComponentInChildren<TMP_Text>(true);

            // Volume 的初始权重由场景设为 0，低落状态到来后再淡入。
            if (moodVolume != null)
            {
                _volumeWeight = moodVolume.weight;
            }

            // 三项状态固定使用场景里的三个物体，不复制、不重新排布。
            _bubbles = new[] { bubble, hungryBubble, tiredBubble };
            _motions = new MoodBubbleMotion[3];
            _bubbleTexts = new TMP_Text[3];
            for (int i = 0; i < _bubbles.Length; i++)
            {
                var item = _bubbles[i];
                if (item == null) continue;
                var motion = item.GetComponent<MoodBubbleMotion>();
                if (motion == null) motion = item.AddComponent<MoodBubbleMotion>();
                _motions[i] = motion;
                _bubbleTexts[i] = i == 0 && bubbleText != null ? bubbleText :
                    item.GetComponentInChildren<TMP_Text>(true);
                SetBubbleText(i);
                motion.HideImmediately();
            }
        }

        void Update()
        {
            Build();
            SubscribeStats();
            RefreshBubbles();
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
            stats.AnyChanged -= OnStatsChanged;
            stats.AnyChanged += OnStatsChanged;
            _subscribedStats = stats;

            // 订阅成功后同步一次当前态（错过边缘也不怕开局已低落）
            RefreshBubbles();
            ApplyLow(stats.IsLowMood);
        }

        void OnStatsChanged(StatChannel channel) => RefreshBubbles();

        void RefreshBubbles()
        {
            if (_subscribedStats == null || _bubbles == null) return;
            for (int i = 0; i < _bubbles.Length; i++)
            {
                if (_motions[i] == null) continue;
                SetBubbleText(i);
                _motions[i].SetVisible(_subscribedStats.IsStatLow((StatKind)i));
            }
        }

        void SetBubbleText(int index)
        {
            string text = index == (int)StatKind.Water ? thirstyText :
                index == (int)StatKind.Satiety ? hungryText : tiredText;
            if (_bubbleTexts[index] != null && _bubbleTexts[index].text != text)
                _bubbleTexts[index].text = text;
        }

        void OnLowMoodChanged(bool low) => ApplyLow(low);

        void ApplyLow(bool low)
        {
            _low = low;
        }

        void OnDestroy()
        {
            if (_subscribedStats != null)
            {
                _subscribedStats.LowMoodChanged -= OnLowMoodChanged;
                _subscribedStats.AnyChanged -= OnStatsChanged;
                _subscribedStats = null;
            }
        }

        /// <summary>R 重开：后处理淡出归零、气泡收起。</summary>
        public void ResetForNewRun()
        {
            _low = false;
            _volumeWeight = 0f;
            if (moodVolume != null) moodVolume.weight = 0f;
            if (_motions != null)
            {
                for (int i = 0; i < _motions.Length; i++)
                {
                    if (_motions[i] != null) _motions[i].HideImmediately();
                }
            }
        }
    }
}
