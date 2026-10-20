using TMPro;
using UnityEngine;

namespace RoomDemo.Mood
{
    /// <summary>以编辑器摆放的位置和外观为基准，表现游移、呼吸和飘散淡出。</summary>
    [DisallowMultipleComponent]
    public class MoodBubbleMotion : MonoBehaviour
    {
        [Header("游移")]
        [Tooltip("相对摆放位置的横向、纵向最大游移距离（本地坐标）")]
        public Vector2 driftAmplitude = new Vector2(0.10f, 0.07f);
        [Min(0f), Tooltip("游移速度，越小越舒缓")]
        public float driftSpeed = 0.35f;

        [Header("呼吸")]
        [Min(0.1f), Tooltip("完成一次放大和缩小所需的秒数")]
        public float breathSeconds = 2.8f;
        [Range(0f, 0.2f), Tooltip("呼吸缩放幅度；0.04 表示围绕原始大小变化 4%")]
        public float breathScale = 0.04f;
        [Range(0f, 0.5f), Tooltip("呼吸时透明度的变化幅度，文字也会同步变化")]
        public float breathOpacity = 0.12f;

        [Header("出现与飘散")]
        [Min(0.01f), Tooltip("出现和消失的渐变时长（秒）")]
        public float fadeSeconds = 0.5f;
        [Tooltip("消失时相对游移位置向外飘散的距离（本地坐标）")]
        public Vector2 disperseOffset = new Vector2(0.16f, 0.22f);

        Vector3 _basePosition;
        Vector3 _baseScale;
        SpriteRenderer[] _sprites;
        Color[] _spriteColors;
        TMP_Text[] _texts;
        Color[] _textColors;
        bool _cached;
        bool _visible;
        float _visibility;
        float _elapsed;
        float _seed;

        void CacheAppearance()
        {
            if (_cached) return;
            _cached = true;
            _basePosition = transform.localPosition;
            _baseScale = transform.localScale;
            _seed = Mathf.Abs(GetInstanceID() % 1000) * 0.137f;
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            _spriteColors = new Color[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++) _spriteColors[i] = _sprites[i].color;
            _texts = GetComponentsInChildren<TMP_Text>(true);
            _textColors = new Color[_texts.Length];
            for (int i = 0; i < _texts.Length; i++) _textColors[i] = _texts[i].color;
        }

        void OnEnable()
        {
            CacheAppearance();
            _elapsed = 0f;
            _visibility = 0f;
            ApplyOpacity(0f);
        }

        public void SetVisible(bool visible)
        {
            CacheAppearance();
            _visible = visible;
            if (visible && !gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void HideImmediately()
        {
            _visible = false;
            _visibility = 0f;
            RestoreAppearance();
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            _elapsed += Time.deltaTime;
            _visibility = Mathf.MoveTowards(_visibility, _visible ? 1f : 0f,
                Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
            if (!_visible && _visibility <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            float noiseTime = _elapsed * Mathf.Max(0f, driftSpeed);
            var drift = new Vector3(
                (Mathf.PerlinNoise(_seed, noiseTime) * 2f - 1f) * driftAmplitude.x,
                (Mathf.PerlinNoise(_seed + 31f, noiseTime) * 2f - 1f) * driftAmplitude.y, 0f);
            float breath = Mathf.Sin(_elapsed * Mathf.PI * 2f / Mathf.Max(0.1f, breathSeconds));
            float fade = Mathf.SmoothStep(0f, 1f, _visibility);
            Vector3 disperse = _visible ? Vector3.zero : (Vector3)(disperseOffset * (1f - fade));
            transform.localPosition = _basePosition + drift + disperse;
            transform.localScale = _baseScale * (1f + breath * breathScale);
            ApplyOpacity(fade * (1f - breathOpacity * (0.5f + breath * 0.5f)));
        }

        void ApplyOpacity(float opacity)
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                if (_sprites[i] == null) continue;
                Color color = _spriteColors[i];
                color.a *= opacity;
                _sprites[i].color = color;
            }
            for (int i = 0; i < _texts.Length; i++)
            {
                if (_texts[i] == null) continue;
                Color color = _textColors[i];
                color.a *= opacity;
                _texts[i].color = color;
            }
        }

        void OnDisable() => RestoreAppearance();

        void RestoreAppearance()
        {
            if (!_cached) return;
            transform.localPosition = _basePosition;
            transform.localScale = _baseScale;
            ApplyOpacity(1f);
        }
    }
}
