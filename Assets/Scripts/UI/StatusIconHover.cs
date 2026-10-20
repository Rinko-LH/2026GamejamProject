using RoomDemo.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoomDemo.UI
{
    /// <summary>状态图标的悬停缩放和轮廓高亮。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class StatusIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("鼠标悬停时相对于原始大小的缩放倍率。")]
        [Min(1f)] public float hoverScale = 1.08f;
        [Tooltip("放大和恢复原始大小所需的秒数，不受游戏暂停影响。")]
        [Min(0f)] public float transitionSeconds = 0.12f;
        [Tooltip("鼠标悬停时的描边颜色。")]
        public Color outlineColor = new Color(1f, 0.68f, 0.08f, 1f);
        [Tooltip("描边在 UI 坐标中的宽度。")]
        [Min(0f)] public float outlineWidth = 2f;

        Outline _outline;
        Vector3 _originalScale, _fromScale, _targetScale;
        float _elapsed;
        bool _pointerInside, _hoverActive;

        void OnEnable()
        {
            _originalScale = transform.localScale;
            _fromScale = _targetScale = _originalScale;
            _pointerInside = _hoverActive = false;
            // 使用独立的描边组件，避免修改图标上可能已有的美术效果。
            if (_outline == null)
            {
                _outline = gameObject.AddComponent<Outline>();
                _outline.useGraphicAlpha = true;
            }
            _outline.enabled = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isActiveAndEnabled) return;
            _pointerInside = true;
            RefreshHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isActiveAndEnabled) return;
            _pointerInside = false;
            RefreshHover();
        }

        void RefreshHover()
        {
            bool dialoguePlaying = Services.Dialogue != null && Services.Dialogue.IsPlaying;
            if (dialoguePlaying)
            {
                _hoverActive = false;
                _outline.enabled = false;
                transform.localScale = _fromScale = _targetScale = _originalScale;
                return;
            }
            bool hovered = _pointerInside;
            if (hovered == _hoverActive) return;
            _hoverActive = hovered;
            _outline.effectColor = outlineColor;
            float width = Mathf.Max(0f, outlineWidth);
            _outline.effectDistance = new Vector2(width, -width);
            _outline.enabled = hovered;
            AnimateTo(hovered ? _originalScale * Mathf.Max(1f, hoverScale) : _originalScale);
        }

        void AnimateTo(Vector3 target)
        {
            _fromScale = transform.localScale;
            _targetScale = target;
            _elapsed = 0f;
            if (transitionSeconds <= 0f) transform.localScale = target;
        }

        void Update()
        {
            RefreshHover();
            _elapsed += Time.unscaledDeltaTime;
            float t = transitionSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / transitionSeconds);
            transform.localScale = Vector3.LerpUnclamped(_fromScale, _targetScale, Mathf.SmoothStep(0f, 1f, t));
        }

        void OnDisable()
        {
            _pointerInside = _hoverActive = false;
            transform.localScale = _originalScale;
            if (_outline != null) _outline.enabled = false;
        }

        void OnDestroy()
        {
            if (_outline == null) return;
            if (Application.isPlaying) Destroy(_outline);
            else DestroyImmediate(_outline);
        }
    }
}
