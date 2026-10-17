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

        /// <summary>切行时触发，参数 null = 播放结束。</summary>
        event Action<DialogueLine> LineChanged;

        /// <summary>队列播完或跳过时触发一次。</summary>
        event Action Finished;
    }
}
