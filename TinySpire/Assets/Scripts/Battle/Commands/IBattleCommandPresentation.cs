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
}
