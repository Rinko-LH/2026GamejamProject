using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomDemo.Contracts
{
    /// <summary>
    /// 进度总线实现：支线完成事件的计数 + 广播。
    /// 单例（同 InputManager 模式）；GameManager.Awake 注册进 Services.Progress，
    /// ResetRun 时调 ResetAll。计数用字典，新增 QuestId 零改代码。
    /// </summary>
    public class ProgressTracker : MonoBehaviour, IProgressTracker
    {
        public static ProgressTracker Instance { get; private set; }

        public event Action<QuestCompletedEvent> QuestCompleted;

        readonly Dictionary<QuestId, int> _counts = new Dictionary<QuestId, int>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Report(QuestId id)
        {
            int n = 0;
            _counts.TryGetValue(id, out n);
            _counts[id] = n + 1;

            float gameTime = 0f;
            var flow = Services.Flow;
            if (flow != null) gameTime = flow.DayElapsedSeconds;
            QuestCompleted?.Invoke(new QuestCompletedEvent(id, gameTime));

            Debug.Log($"[进度] 支线完成：{id}（累计 {_counts[id]} 次）");
        }

        public int Count(QuestId id)
        {
            int n = 0;
            _counts.TryGetValue(id, out n);
            return n;
        }

        public void ResetAll()
        {
            _counts.Clear();
        }
    }
}
