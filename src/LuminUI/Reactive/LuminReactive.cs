using System;

namespace LuminUI
{
    /// <summary>
    /// MVR 的 Reactive 层基类。实例由框架池化；生成代码负责把它连接到 Model，
    /// 多个 View/Widget 可以共享同一个实例，但都不能直接取得 Model。
    /// </summary>
    public abstract class LuminReactive
    {
        public bool IsAttached { get; private set; }

        /// <summary>框架调用；生成类型在这里连接新的 Model。</summary>
        public void __Attach(object model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            OnAttach(model);
            IsAttached = true;
        }

        /// <summary>框架调用；回池前释放 Model 引用。</summary>
        public void __Detach()
        {
            if (!IsAttached) return;
            OnDetach();
            IsAttached = false;
        }

        protected abstract void OnAttach(object model);
        protected abstract void OnDetach();
    }
}
