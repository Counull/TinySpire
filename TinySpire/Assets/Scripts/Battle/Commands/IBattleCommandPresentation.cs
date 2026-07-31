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
    /// M4C 生产接线使用的无动画表现 adapter；未来真实表现层可替换该注册而不绕过队列。
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
