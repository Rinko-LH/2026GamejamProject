using RoomDemo.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo.UI
{
    /// <summary>将三项状态值映射到场景中圆形 Image 的填充量。</summary>
    [DisallowMultipleComponent]
    public sealed class StatusCircleDisplay : MonoBehaviour
    {
        [Header("状态填充图")]
        [Tooltip("水分圆形，对应水分值 0～100。可在 Image 上调整填充方向。")]
        public Image waterFill;
        [Tooltip("饥饿图标的填充图，对应饱食度 0～100；饱食度越高，填充越满。")]
        public Image satietyFill;
        [Tooltip("精神圆形，对应精神值 0～100。可在 Image 上调整填充方向。")]
        public Image spiritFill;

        [Header("低落警告")]
        [Tooltip("某一状态低于自身阈值时，该填充图使用此颜色。默认是 #C13F2F。")]
        public Color lowColor = new Color32(0xC1, 0x3F, 0x2F, 0xFF);
        [Tooltip("一次完整呼吸闪烁的秒数，使用真实时间，不受游戏时间缩放影响。")]
        [Min(0.1f)] public float breathSeconds = 1.6f;
        [Tooltip("呼吸最暗时的透明度倍率。最大透明度保持原值，不会完全闪灭。")]
        [Range(0f, 1f)] public float minimumBreathAlpha = 0.35f;

        IStatSource _stats;
        Color _waterColor, _satietyColor, _spiritColor;

        void OnEnable()
        {
            _waterColor = waterFill != null ? waterFill.color : Color.white;
            _satietyColor = satietyFill != null ? satietyFill.color : Color.white;
            _spiritColor = spiritFill != null ? spiritFill.color : Color.white;
            Configure(waterFill);
            Configure(satietyFill);
            Configure(spiritFill);
            BindStats();
            Refresh();
        }

        // 服务可能在本组件启用之后才注册，重开或切换服务时也要重新绑定。
        void Update()
        {
            BindStats();
            UpdateWarningColors(Time.unscaledTime);
        }

        void BindStats()
        {
            var next = Services.Stats;
            if (ReferenceEquals(_stats, next)) return;
            if (_stats != null) _stats.AnyChanged -= OnStatChanged;
            _stats = next;
            if (_stats != null) _stats.AnyChanged += OnStatChanged;
            Refresh();
        }

        void OnStatChanged(StatChannel channel) => Refresh();

        static void Configure(Image image)
        {
            if (image == null) return;
            image.type = Image.Type.Filled;
            image.raycastTarget = false;
        }

        void Refresh()
        {
            SetFill(waterFill, _stats != null ? _stats.WaterValue : 0f);
            SetFill(satietyFill, _stats != null ? _stats.SatietyValue : 0f);
            SetFill(spiritFill, _stats != null ? _stats.SpiritValue : 0f);
            UpdateWarningColors(Time.unscaledTime);
        }

        void UpdateWarningColors(float time)
        {
            float wave = 0.5f + 0.5f * Mathf.Cos(time * Mathf.PI * 2f / Mathf.Max(0.1f, breathSeconds));
            float alpha = Mathf.Lerp(Mathf.Clamp01(minimumBreathAlpha), 1f, wave);
            SetWarningColor(waterFill, _waterColor, StatKind.Water, alpha);
            SetWarningColor(satietyFill, _satietyColor, StatKind.Satiety, alpha);
            SetWarningColor(spiritFill, _spiritColor, StatKind.Spirit, alpha);
        }

        void SetWarningColor(Image image, Color normal, StatKind kind, float alpha)
        {
            if (image == null) return;
            Color color = normal;
            if (_stats != null && _stats.IsStatLow(kind))
            {
                color = lowColor;
                color.a *= normal.a * alpha;
            }
            image.color = color;
        }

        static void SetFill(Image image, float value)
        {
            if (image != null) image.fillAmount = Mathf.Clamp01(value / 100f);
        }

        void OnDisable()
        {
            if (_stats != null) _stats.AnyChanged -= OnStatChanged;
            _stats = null;
            if (waterFill != null) waterFill.color = _waterColor;
            if (satietyFill != null) satietyFill.color = _satietyColor;
            if (spiritFill != null) spiritFill.color = _spiritColor;
        }
    }
}
