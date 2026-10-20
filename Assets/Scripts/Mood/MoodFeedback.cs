using RoomDemo.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoomDemo.Mood
{
    /// <summary>
    /// 低落表现（S8，策划案 5.4）：任一状态低于阈值时，淡入 URP 全局后处理，
    /// 通过 Volume 权重降低饱和度并增加暗角；低落解除或重开时淡出。
    /// 移速变化由移动脚本读取 Services.Stats.IsLowMood 处理。
    /// 玩家头侧的气泡改为手动在场景中摆好（MoodBubble 物体），本组件只负责
    /// 低落时显隐（SetActive），不再动态生成。
    /// </summary>
    public class MoodFeedback : MonoBehaviour, IResettable
    {
        [Header("引用（装配注入）")]
        [Tooltip("配置了 Color Adjustments 与 Vignette 的全局 Volume")]
        public Volume moodVolume;

        [Tooltip("手动摆好的气泡物体（Player 子物体，本组件只负责低落时显隐）")]
        public GameObject bubble;

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

            // 手动摆好的气泡默认应为隐藏，这里同步一次初始状态。
            if (bubble != null)
            {
                bubble.SetActive(_low);
            }
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
            if (bubble != null && bubble.activeSelf != low)
                bubble.SetActive(low);
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
            if (bubble != null) bubble.SetActive(false);
        }
    }
}