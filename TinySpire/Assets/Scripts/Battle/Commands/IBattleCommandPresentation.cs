using System;

namespace TinySpire.Battle
{
    /// <summary>
    /// 权威执行结果与表现完成信号之间的 adapter seam。
    /// </summary>
    public interface IBattleCommandPresentation
    {
        /// <summary>按权威顺序展示执行结果，并在表现完成时调用完成回调。</summary>
        void Present(BattleCommandExecutionResult result, Action onCompleted);
    }

    /// <summary>
    /// 零等待测试使用的即时表现 adapter；M4D 生产接线改用可观察反馈 adapter。
    /// </summary>
    public sealed class ImmediateBattleCommandPresentation : IBattleCommandPresentation
    {
        /// <summary>确认执行结果有效后立即回报完成，使队列可以处理下一条权威命令。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (onCompleted == null)
                throw new ArgumentNullException(nameof(onCompleted));

            onCompleted.Invoke();
        }
    }
}
