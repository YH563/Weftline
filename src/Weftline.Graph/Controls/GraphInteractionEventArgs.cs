using Weftline.Models;

namespace Weftline.Graph.Controls;

/// <summary>
/// 双击模块时触发，携带被激活的模块节点。
/// Raised when a module is double-clicked, carrying the activated node.
/// </summary>
public sealed class ModuleActivatedEventArgs(GraphModuleNode module) : EventArgs
{
    public GraphModuleNode Module { get; } = module;
}

/// <summary>
/// 双击模块边时触发，携带被激活的模块边。
/// Raised when a module edge is double-clicked, carrying the activated edge.
/// </summary>
public sealed class ModuleEdgeActivatedEventArgs(GraphModuleEdge edge) : EventArgs
{
    public GraphModuleEdge Edge { get; } = edge;
}

/// <summary>
/// 双击章节聚合边时触发，携带被激活的章节边及底层模块分支。
/// Raised when a chapter edge is double-clicked, carrying the aggregated edge.
/// </summary>
public sealed class ChapterEdgeActivatedEventArgs(ChapterEdge edge) : EventArgs
{
    public ChapterEdge Edge { get; } = edge;
}

/// <summary>
/// 悬停模块变化时触发；离开模块时 Module 为 null。
/// Raised when the hovered module changes; Module is null when leaving modules.
/// </summary>
public sealed class ModuleHoverChangedEventArgs(GraphModuleNode? module) : EventArgs
{
    public GraphModuleNode? Module { get; } = module;
}
