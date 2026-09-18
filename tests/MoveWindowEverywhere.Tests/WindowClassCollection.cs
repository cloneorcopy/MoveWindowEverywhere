using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 涉及窗口类注册的测试必须串行执行。
/// 窗口类按进程登记，多个测试并行地注册和注销同名类会产生竞态：
/// 其中一个测试注销类之后，另一个测试的 CreateWindowEx 会因为类已不存在而失败。
/// 把相关测试类归入本集合即可消除并行。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WindowClassCollection
{
    public const string Name = "窗口类注册";
}
