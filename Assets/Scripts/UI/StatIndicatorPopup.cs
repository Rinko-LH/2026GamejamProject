using RoomDemo.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo.UI
{
    /// <summary>单项状态：展开、回落到数值、停留、模糊淡出。</summary>
    [DisallowMultipleComponent]
    public sealed class StatIndicatorPopup : MonoBehaviour, IResettable
    {
        [Header("状态与引用")]
        [Tooltip("此图标对应的状态。饥饿图标使用 Satiety（饱食度），数值越大越满。")]
        public StatKind statKind;
        [Tooltip("状态图标按钮，不放在淡出的指示条容器中。对话播放期间点击无效。")]
        public Button iconButton;
        [Tooltip("指示条容器上的 CanvasGroup，只淡出底板、白条和填充条。")]
        public CanvasGroup indicatorGroup;
        [Tooltip("展开后保持满值的底板。")]
        public Image backplate;
        [Tooltip("展开后保持满值的白条。")]
        public Image whiteBar;
        [Tooltip("先展开到 1，再回落到当前状态百分比的填充条。")]
        public Image fillBar;

        [Header("动画时长（真实秒）")]
        [Tooltip("三张图从当前填充量快速展开到 1 所需的秒数。")]
        [Min(0f)] public float revealSeconds = 0.22f;
        [Tooltip("填充条从 1 回落到当前数值所需的秒数。")]
        [Min(0f)] public float settleSeconds = 0.6f;
        [Tooltip("填充条到位后保持显示的秒数。")]
        [Min(0f)] public float holdSeconds = 4f;
        [Tooltip("整组指示条渐变透明并模糊消失所需的秒数。")]
        [Min(0f)] public float fadeSeconds = 0.7f;
        [Tooltip("展开和数值回落共用的缓入缓出曲线。")]
        public AnimationCurve fillCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("淡出结束时的纹理模糊采样半径。0 表示只淡出。")]
        [Range(0f, 10f)] public float fadeBlurRadius = 4f;

        [Header("单项低落警告")]
        [Tooltip("低落时填充条的目标颜色，默认 #C13F2F。")]
        public Color lowColor = new Color32(0xC1, 0x3F, 0x2F, 0xFF);
        [Tooltip("一次完整呼吸的秒数，不受游戏时间缩放影响。")]
        [Min(0.1f)] public float breathSeconds = 1.6f;
        [Tooltip("呼吸最低透明度倍率。呼吸在数值回落过程中渐渐加入。")]
        [Range(0f, 1f)] public float minimumBreathAlpha = 0.35f;

        Image[] _images;
        Material[] _originalMaterials;
        readonly float[] _revealFrom = new float[3];
        Color _normalColor;
        Material _blurMaterial;
        Button _boundButton;
        IStatSource _stats;
        bool _wasLow;
        float _time = -1f, _breathTime, _revealAlpha;
        static readonly int BlurRadius = Shader.PropertyToID("_BlurRadius");

        bool Initialize()
        {
            if (_images != null) return true;
            if (backplate == null || whiteBar == null || fillBar == null || indicatorGroup == null) return false;
            _images = new[] { backplate, whiteBar, fillBar };
            _originalMaterials = new Material[3];
            _normalColor = fillBar.color;
            for (int i = 0; i < _images.Length; i++)
            {
                _originalMaterials[i] = _images[i].material;
                _images[i].type = Image.Type.Filled;
                _images[i].raycastTarget = false;
            }
            indicatorGroup.interactable = false;
            indicatorGroup.blocksRaycasts = false;
            return true;
        }

        void OnEnable()
        {
            if (Initialize()) ResetForNewRun();
            Bind();
        }

        void Update()
        {
            Bind();
            AdvanceAnimation(Time.unscaledDeltaTime);
        }

        void Bind()
        {
            if (_boundButton != iconButton)
            {
                if (_boundButton != null) _boundButton.onClick.RemoveListener(OnIconClicked);
                _boundButton = iconButton;
                if (_boundButton != null)
                {
                    _boundButton.navigation = new Navigation { mode = Navigation.Mode.None };
                    _boundButton.onClick.AddListener(OnIconClicked);
                }
            }
            var next = Services.Stats;
            if (ReferenceEquals(_stats, next)) return;
            if (_stats != null) _stats.AnyChanged -= OnStatChanged;
            _stats = next;
            _wasLow = false;
            if (_stats != null)
            {
                _stats.AnyChanged += OnStatChanged;
                CheckLowTransition();
            }
        }

        void OnStatChanged(StatChannel channel)
        {
            if (channel.Kind == statKind) CheckLowTransition();
        }

        void CheckLowTransition()
        {
            bool low = _stats != null && _stats.IsStatLow(statKind);
            bool enteredLow = low && !_wasLow;
            _wasLow = low;
            if (enteredLow) Show();
        }

        float CurrentFill => _stats == null ? 0f : Mathf.Clamp01((statKind == StatKind.Water
            ? _stats.WaterValue : statKind == StatKind.Satiety ? _stats.SatietyValue : _stats.SpiritValue) / 100f);

        void OnIconClicked()
        {
            if (Services.Dialogue != null && Services.Dialogue.IsPlaying) return;
            Show();
        }

        public void Show()
        {
            if (!isActiveAndEnabled || !Initialize()) return;
            RestoreMaterials();
            for (int i = 0; i < _images.Length; i++) _revealFrom[i] = _images[i].fillAmount;
            _revealAlpha = indicatorGroup.alpha;
            _time = _breathTime = 0f;
            fillBar.color = _normalColor;
            AdvanceAnimation(0f);
        }

        float Ease(float t) => Mathf.Clamp01(fillCurve == null ? Mathf.SmoothStep(0f, 1f, t) : fillCurve.Evaluate(t));

        void AdvanceAnimation(float deltaTime)
        {
            if (_time < 0f || !Initialize()) return;
            float dt = Mathf.Max(0f, deltaTime);
            _time += dt;
            _breathTime += dt;
            float reveal = Mathf.Max(0f, revealSeconds), settle = Mathf.Max(0f, settleSeconds);
            float hold = Mathf.Max(0f, holdSeconds), fade = Mathf.Max(0f, fadeSeconds);
            float settledAt = reveal + settle;
            float fadeAt = settledAt + hold;
            if (_time < reveal)
            {
                float t = Ease(_time / reveal);
                for (int i = 0; i < _images.Length; i++) _images[i].fillAmount = Mathf.Lerp(_revealFrom[i], 1f, t);
                indicatorGroup.alpha = Mathf.Lerp(_revealAlpha, 1f, t);
                return;
            }
            backplate.fillAmount = whiteBar.fillAmount = 1f;
            indicatorGroup.alpha = 1f;
            float colorBlend = 1f;
            if (_time < settledAt)
            {
                colorBlend = Ease((_time - reveal) / settle);
                fillBar.fillAmount = Mathf.Lerp(1f, CurrentFill, colorBlend);
            }
            else fillBar.fillAmount = CurrentFill;
            ApplyWarning(colorBlend);
            if (_time < fadeAt) return;
            if (_time >= fadeAt + fade) { ResetForNewRun(); return; }
            float fadeT = Mathf.SmoothStep(0f, 1f, (_time - fadeAt) / fade);
            indicatorGroup.alpha = 1f - fadeT;
            ApplyBlur(fadeT * Mathf.Max(0f, fadeBlurRadius));
        }

        void ApplyWarning(float blend)
        {
            bool low = _stats != null && _stats.IsStatLow(statKind);
            Color color = Color.Lerp(_normalColor, low ? lowColor : _normalColor, blend);
            float wave = 0.5f + 0.5f * Mathf.Cos(_breathTime * Mathf.PI * 2f / Mathf.Max(0.1f, breathSeconds));
            float pulse = Mathf.Lerp(Mathf.Clamp01(minimumBreathAlpha), 1f, wave);
            color.a = _normalColor.a * (low ? Mathf.Lerp(1f, pulse, blend) : 1f);
            fillBar.color = color;
        }

        void ApplyBlur(float radius)
        {
            if (_blurMaterial == null)
            {
                var shader = Resources.Load<Shader>("StatusIndicatorBlur");
                if (shader == null) return;
                _blurMaterial = new Material(shader) { name = name + " Indicator Fade", hideFlags = HideFlags.HideAndDontSave };
            }
            _blurMaterial.SetFloat(BlurRadius, radius);
            foreach (var image in _images) image.material = _blurMaterial;
        }

        void RestoreMaterials()
        {
            if (_images == null) return;
            for (int i = 0; i < _images.Length; i++)
                if (_images[i] != null) _images[i].material = _originalMaterials[i];
        }

        public void ResetForNewRun()
        {
            _time = -1f;
            if (!Initialize()) return;
            foreach (var image in _images) image.fillAmount = 0f;
            indicatorGroup.alpha = 0f;
            fillBar.color = _normalColor;
            RestoreMaterials();
        }

        void OnDisable()
        {
            if (_boundButton != null) _boundButton.onClick.RemoveListener(OnIconClicked);
            _boundButton = null;
            if (_stats != null) _stats.AnyChanged -= OnStatChanged;
            _stats = null;
            _wasLow = false;
            ResetForNewRun();
        }

        void OnDestroy()
        {
            if (_blurMaterial == null) return;
            if (Application.isPlaying) Destroy(_blurMaterial);
            else DestroyImmediate(_blurMaterial);
        }
    }
}
