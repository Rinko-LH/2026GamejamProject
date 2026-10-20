using UnityEngine;

namespace RoomDemo
{
    /// <summary>单块画板：绑定画布、排布画布尺寸并提供绘画进度查询。</summary>
    public class PaintingBoard : MonoBehaviour
    {
        [Header("标识")]
        [Tooltip("第几幅画，1~3")]
        public int boardIndex = 1;

        [Header("画布引用")]
        public DrawingCanvas canvas;
        public SpriteRenderer canvasRenderer;

        /// <summary>是否已达到作画时间要求；达标后仍可继续绘画。</summary>
        public bool IsDone => canvas != null && canvas.IsComplete;

        /// <summary>是否已落笔。</summary>
        public bool Started => canvas != null && canvas.TimerStarted;

        /// <summary>剩余作画秒数。</summary>
        public float RemainingSeconds => canvas != null ? canvas.RemainingSeconds : 0f;

        /// <summary>着墨量 0~1，仅用于标签展示。</summary>
        public float InkRatio => canvas != null ? canvas.InkRatio : 0f;

        void Awake() => EnsureVisuals();

        /// <summary>启动时重新绑定由 DrawingCanvas 生成的画布纹理。</summary>
        public void EnsureVisuals()
        {
            if (canvas != null && canvasRenderer != null) canvas.Bind(canvasRenderer);
        }

        /// <summary>按指定尺寸排布画布，补偿纹理宽高比。</summary>
        public void Layout(float boardWidth, float boardHeight)
        {
            if (canvasRenderer == null) return;
            float spriteHeight = canvas != null
                ? (float)canvas.canvasHeight / Mathf.Max(1, canvas.canvasWidth)
                : 1f;
            canvasRenderer.transform.localPosition = Vector3.zero;
            canvasRenderer.transform.localScale = new Vector3(boardWidth, boardHeight / spriteHeight, 1f);
        }

        /// <summary>特写里显示，回房间时隐藏。</summary>
        public void SetVisible(bool visible)
        {
            if (canvasRenderer != null) canvasRenderer.enabled = visible;
        }

        /// <summary>重置画布和作画计时。</summary>
        public void ResetBoard()
        {
            if (canvas != null) canvas.ResetPainting();
        }
    }
}
