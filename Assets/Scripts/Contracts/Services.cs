using UnityEngine;

namespace RoomDemo.Contracts
{
    /// <summary>
    /// 服务定位器（契约层）：跨系统交互的唯一入口。
    ///
    /// 铁律（详见 docs/系统实施规范.md）：
    /// - 新域（UI/World/Quest/Mood/Ending）跨域消费一律读 Services.Xxx，
    ///   禁止 FindObjectOfType 其他域的具体类、禁止互相持引用；
    /// - 核心域（RoomDemo 根：GameManager/PlayerStats/DayTimer/DrawingBoardInteract/
    ///   ConsumablePickup/BrushPickup/RoomInteractable）内部允许直连（既有模式）；
    /// - Quest 域内（S3~S6）允许域内直连；
    /// - 注册时机：GameManager.Awake 统一赋值；消费方全部判空（启动帧可能未就绪）。
    /// </summary>
    public static class Services
    {
        public static IStatSource Stats;
        public static IGameFlow Flow;
        public static IDialogueService Dialogue;
        public static IProgressTracker Progress;
    }
}
