/* Author:  Leonardo Trevisan Silio
 * Date:    09/10/2024
 */
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Radiance.Renders;

using Factories;

using Buffers;
using Shaders;
using Shaders.Objects;
using Shaders.Dependencies;
using Exceptions;

/// <summary>
/// A render that unite many similar render callings in only once calling.
/// </summary>
public class MultiRender(
    Delegate function,
    params object[] curryingParams
    ) : Render(function, curryingParams)
{
    List<object> factories = [ ];
    readonly SimpleBuffer buffer = new();
    IBufferedData? lastBuffer = null;
    Func<int, bool> breaker = i => i < 1;
    bool dataChanges = true;
    
    /// <summary>
    /// Set the function that decides when the render need stop.
    /// </summary>
    public MultiRender SetBreaker(Func<int, bool> breaker)
    {
        dataChanges = true;
        this.breaker = breaker;
        return this;
    }

    public override MultiRender Curry(params object?[] args)
    {
        return new(function, [ ..curryingArguments, ..DisplayValues(args) ])
        {
            Context = Context,
            Dependences = Dependences,
            factories = [ ..factories, ..args.Where(arg => arg is RenderParameterFactory) ],
            breaker = breaker
        };
    }
    protected override IBufferedData FillData(IBufferedData buffer)
    {
        if (lastBuffer != buffer)
        {
            dataChanges = true;
            lastBuffer = buffer;
        }

        if (!dataChanges)
            return this.buffer;
        dataChanges = false;

        var vertexes = buffer.Triangulation.Data;
        UpdateData(vertexes);

        return this.buffer;
    }

    void UpdateData(float[] basicVertexes)
    {
        buffer.Clear();

        RenderParameterFactory[] computations = factories
            .Where(c => c is RenderParameterFactory)
            .Select(c => (RenderParameterFactory)c)
            .ToArray();
        float[] computationResult = new float[computations.Length];

        int i;
        for (i = 0; breaker(i); i++)
        {
            for (int j = 0; j < computationResult.Length; j++)
                computations[j].GenerateData(i, computationResult, j);
            
            for (int k = 0; k < basicVertexes.Length; k += 3)
            {
                buffer.Add(basicVertexes[k + 0]);
                buffer.Add(basicVertexes[k + 1]);
                buffer.Add(basicVertexes[k + 2]);
                for (int j = 0; j < computationResult.Length; j++)
                    buffer.Add(computationResult[j]);
            }
        }
        
        buffer.Vertices = i * basicVertexes.Length / 3;
    }

    int layoutLocations = 1;
    protected override ShaderObject GenerateDependence(ParameterInfo parameter, int index, object?[] curriedValues)
    {
        ArgumentNullException.ThrowIfNull(parameter, nameof(parameter));

        var name = parameter.Name!;
        var isFloat = parameter.ParameterType == typeof(FloatShaderObject);
        var isTexture = parameter.ParameterType == typeof(Sampler2DShaderObject);
        var isConstant = index < curriedValues.Length;
        var isFactory = isConstant && curriedValues[index] is RenderParameterFactory;
        
        return (isFloat, isTexture, isConstant, isFactory) switch
        {
            (true, false, true, true) => new FloatShaderObject(
                name, ShaderOrigin.VertexShader, [ new FloatBufferDependence(name, layoutLocations++) ]
            ),

            (true, false, true, false) => new FloatShaderObject(
                name, ShaderOrigin.FragmentShader, [ new ConstantDependence(name, 
                    curriedValues[index] is float value ? value : throw new Exception($"{curriedValues[index]} is not a float.")) ]
            ),

            (true, false, false, false) => new FloatShaderObject(
                name, ShaderOrigin.Global, [ new UniformFloatDependence(name) ]
            ),

            (false, true, _, false) => new Sampler2DShaderObject(
                name, ShaderOrigin.FragmentShader, [ new TextureDependence(name) ]
            ),

            (false, true, _, true) => throw new NotImplementedException(
                "Radiance not work with texture buffer yet. You cannot use a factory to draw many textures."
            ),

            _ => throw new InvalidRenderException(parameter)
        };
    }
}