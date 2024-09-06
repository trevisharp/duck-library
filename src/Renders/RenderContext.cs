/* Author:  Leonardo Trevisan Silio
 * Date:    04/09/2024
 */
using System;
using System.Threading;
using System.Collections.Generic;

namespace Radiance.Renders;

using Shaders;
using Shaders.Objects;
using Primitives;

/// <summary>
/// A Thread-Safe global context data object.
/// </summary>
public class RenderContext
{
    static readonly Dictionary<int, RenderContext> threadMap = [];

    static int GetCurrentThreadId()
    {
        var crr = Thread.CurrentThread;
        var id  = crr.ManagedThreadId;
        return id;
    }

    /// <summary>
    /// Open a new context for this thread.
    /// </summary>
    public static void OpenContext()
    {
        CloseContext();

        var id = GetCurrentThreadId();
        threadMap.Add(id, new());
    }

    /// <summary>
    /// Close the context for this thread.
    /// </summary>
    public static void CloseContext()
    {
        var ctx = GetContext();
        if (ctx is null)
            return;

        foreach (var x in ctx.CallHistory)
        {
            Console.WriteLine(x);
        }

        var id = GetCurrentThreadId();
        threadMap.Remove(id);
    }

    /// <summary>
    /// Get the opened context for this thread or null if it is closed.
    /// </summary>
    public static RenderContext? GetContext()
    {
        var id = GetCurrentThreadId();
        return threadMap.TryGetValue(id, out var ctx)
            ? ctx : null;
    }

    public bool Verbose { get; set; } = false;

    public Vec3ShaderObject Position { get; set; } = new("pos", ShaderOrigin.VertexShader, [ Utils.bufferDep ]);

    public Vec4ShaderObject Color { get; set; } = new("vec4(0.0, 0.0, 0.0, 1.0)", ShaderOrigin.FragmentShader, []);

    public List<object> CallHistory { get; private set; } = [];

    public void RegisterCall(Render render, object[] arguments)
        => CallHistory.Add(new RenderCall(render, arguments));
    
    public void RegisterEndRender(Render render)
        => CallHistory.Add(new RenderEnd(render));

    public void AddClear(Vec4 color)
        => CallHistory.Add(new Clear(color));

    public void AddDraw()
        => CallHistory.Add(new Draw());

    public void AddFill()
        => CallHistory.Add(new Fill());

    public void AddStrip()
        => CallHistory.Add(new Strip());

    public void AddFan()
        => CallHistory.Add(new Fan());

    public void AddLines()
        => CallHistory.Add(new Lines());

    public record RenderCall(Render Render, object[] Arguments);
    public record RenderEnd(Render Render);
    public record Clear(Vec4 Color);
    public record Draw;
    public record Fill;
    public record Strip;
    public record Fan;
    public record Lines;
}