using System;

namespace RoomDemo.Contracts
{
    /// <summary>
    /// 对话服务契约（策划案 1.5）：DialogueManager 实现。
    /// 行结构直接复用核心域 DialogueLine（可序列化，装配菜单要填）。
    /// </summary>
    public interface IDialogueService
    {
        bool IsPlaying { get; }
        void Play(DialogueLine[] lines);
        void Stop();

        /// <summary>
        /// 把当前行重新派发一次 LineChanged（不改播放进度、不重置计时）。
        ///
        /// 用途：UI 是<b>迟到</b>的订阅者时补上已错过的行——典型场景是
        /// GameManager.Start 就派发了开场第一行，而 DialogueBoxUI.Start 还没订阅，
        /// 导致开场首句整段消失（P6）。订阅完成后调这个就能补回来。
        /// 未播放时无操作。
        /// </summary>
        void ReplayCurrent();

        /// <summary>切行时触发，参数 null = 播放结束。</summary>
        event Action<DialogueLine> LineChanged;

        /// <summary>队列播完或跳过时触发一次。</summary>
        event Action Finished;
    }
}
