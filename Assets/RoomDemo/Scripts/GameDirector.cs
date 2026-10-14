using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 游戏总控：把状态系统、时间系统、画板串起来，并管理场景搭建入口。
    ///
    /// 美术资源后期接入时，这里是唯一需要改的装配点。
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        [Header("系统引用")]
        public PlayerStats stats;
        public DayTimer timer;
        public DrawingBoardInteract board;

        [Header("结算")]
        public int paintingsCompleted;

        void Awake()
        {
            // 保险起见兜底找一次，避免场景里漏挂引用
            if (stats == null) stats = FindObjectOfType<PlayerStats>();
            if (timer == null) timer = FindObjectOfType<DayTimer>();
            if (board == null) board = FindObjectOfType<DrawingBoardInteract>();
        }

        void Start()
        {
            if (board != null)
                board.PaintingCompleted += OnPaintingCompleted;

            if (timer != null)
                timer.DayEnded += OnDayEnded;

            // 开场：告知主线任务 + 播报状态
            Debug.Log("[开场] 「今天要画画来着」");
            if (stats != null)
                Debug.Log($"[状态] 水分 {stats.Water.Value:F0}%，饥饿 {stats.Satiety.Value:F0}%，精神 {stats.Spirit.Value:F0}%");
        }

        void OnDestroy()
        {
            if (board != null) board.PaintingCompleted -= OnPaintingCompleted;
            if (timer != null) timer.DayEnded -= OnDayEnded;
        }

        void OnPaintingCompleted(int index)
        {
            paintingsCompleted++;
            if (paintingsCompleted >= (board != null ? board.canvas.totalPaintings : 3))
                Debug.Log("[结算] 全部画作完成（结局逻辑待补充）");
        }

        void OnDayEnded()
        {
            Debug.Log("[结算] 一天结束（结局逻辑待补充）");
        }

        /// <summary>调试用：重置整局。</summary>
        public void ResetRun()
        {
            paintingsCompleted = 0;
            if (stats != null) stats.ResetStats();
            if (timer != null) timer.ResetTimer();
            if (board != null && board.canvas != null) board.canvas.ResetAllPaintings();
        }
    }
}