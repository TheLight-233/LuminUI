using System.Threading;
using LuminThread;

namespace LuminUI.Interface
{
    // 资源加载职责：把屏的预制体加载/实例化成一个平台根节点，以及卸载、预热。
    // 节点本身的显隐、层内排序属于 IUiBridge（视图操作），不在这里。
    public interface IUiLoader
    {
        // 加载并实例化屏根节点，返回平台节点；支持取消。
        LuminTask<object> LoadAsync(string screenName, UILayer layer, CancellationToken ct = default);

        // 预热资源到内存但不实例化，之后 OpenAsync 可秒开。
        LuminTask PreloadAsync(string screenName, UILayer layer, CancellationToken ct = default);

        // 销毁屏根节点并释放资源（资源句柄引用计数由实现负责）。
        void Unload(object root);
    }
}
