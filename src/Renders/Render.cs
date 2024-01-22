/* Author:  Leonardo Trevisan Silio
 * Date:    21/01/2024
 */
using System;
using System.Dynamic;

namespace Radiance.Renders;

using Data;
using Internal;
using Exceptions;

using Shaders.Objects;
using Radiance.Shaders.Dependencies;

/// <summary>
/// Represents a function that can used by GPU to draw in the screen.
/// </summary>
public class Render : DynamicObject
{
    private OpenGLManager manager;
    private readonly int extraParameterCount;
    public int ExtraParameterCount => extraParameterCount;

    public Render(Action function)
    {
        this.extraParameterCount = 0;
        Window.RunOrSchedule(() => {
            initRender();
            function();
        });
    }

    public Render(Action<FloatShaderObject> function)
    {
        this.extraParameterCount = 1;
    }

    public Render(Action<FloatShaderObject, FloatShaderObject> function)
    {
        this.extraParameterCount = 2;
    }

    public Render(Action<FloatShaderObject,
        FloatShaderObject, FloatShaderObject> function)
    {
        this.extraParameterCount = 3;
    }

    public Render(Action<FloatShaderObject, FloatShaderObject,
        FloatShaderObject, FloatShaderObject> function)
    {
        this.extraParameterCount = 4;
        this.manager = new OpenGLManager();
    }

    public override bool TryInvoke(
        InvokeBinder binder, object[] args, out object result)
    {
        if (args.Length == 0)
            throw new MissingPolygonException();

        var poly = args[0] as Polygon;
        if (poly is null)
            throw new MissingPolygonException();

        var data = getArgs(args[1..]);
        manager.Render(poly, data);

        result = true;
        return true;
    }

    private void initRender()
    {
        var ctx = RenderContext.CreateContext();
        ctx.Position = new BufferDependence<Vec3ShaderObject>(
            "pos", null, 0
        );
        ctx.Color = new Vec4ShaderObject("(0.0, 0.0, 0.0, 1.0)");
        this.manager = ctx.Manager = new OpenGLManager();
    }

    private float[] getArgs(object[] args)
    {
        int index = 0;
        var result = new float[extraParameterCount];

        foreach (var arg in args)
            index = setArgs(arg, result, index);
        
        if (index < args.Length)
            throw new MissingParametersException();
        
        return result;
    }

    private int setArgs(object arg, float[] arr, int index)
    {
        switch (arg)
        {
            case float num:
                add(num);
                break;
                
            case Vec2 vec:
                add(vec.X);
                add(vec.Y);
                break;
                
            case Vec3 vec:
                add(vec.X);
                add(vec.Y);
                add(vec.Z);
                break;
                
            case Vec4 vec:
                add(vec.X);
                add(vec.Y);
                add(vec.Z);
                add(vec.W);
                break;
        }
        return index;

        void add(float value)
        {
            if (index >= arr.Length)
                throw new SurplusParametersException();
            arr[index++] = value;
        }
    }
}