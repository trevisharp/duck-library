/* Author:  Leonardo Trevisan Silio
 * Date:    29/08/2024
 */
using System;
using System.Linq;
using System.Collections.Generic;

namespace Radiance.Shaders;

using Dependencies;
using static ShaderOrigin;

/// <summary>
/// Represents any data in a shader implementation.
/// </summary>
public abstract class ShaderObject(
    ShaderType type,
    string expression,
    ShaderOrigin origin,
    IEnumerable<ShaderDependence> dependencies
) {
    public readonly ShaderType Type = type;
    public readonly string Expression = expression;
    public readonly ShaderOrigin Origin = origin;
    public readonly IEnumerable<ShaderDependence> Dependencies = dependencies;

    public override string ToString()
        => Expression;
    
    public static R Union<R>(string newExpression, params ShaderObject[] objs)
        where R : ShaderObject
    {
        var deps = objs.SelectMany(x => x.Dependencies);
        var originInfo = unionOrigin(objs.Select(x => x.Origin));

        if (originInfo.hasConflitct)
        {
            foreach (var vertObj in objs.Where(x => x.Origin == VertexShader))
            {
                var output = new OutputDependence(vertObj);
                newExpression = newExpression
                    .Replace(vertObj.Expression, output.Name);
                deps = deps.Append(output);
            }
        }

        var newObj = Activator.CreateInstance(
            typeof(R), newExpression, originInfo.origin, deps
        ) as R;
        return newObj!;
    }

    private static (ShaderOrigin origin, bool hasConflitct) unionOrigin(IEnumerable<ShaderOrigin> origins)
    {
        var nonGlobal =
            from origin in origins
            where origin != Global
            select origin;
        
        var hasVertex = nonGlobal.Contains(VertexShader);
        var hasFragment = nonGlobal.Contains(FragmentShader);

        if (hasFragment)
            return (FragmentShader, hasVertex);
        
        if (hasVertex)
            return (VertexShader, false);
        
        return (Global, false);
    }

    public static R Transform<T, R>(string newExpression, T obj)
        where T : ShaderObject
        where R : ShaderObject
    {
        var newObj = Activator.CreateInstance(
            typeof(R), newExpression,
            obj.Origin, obj.Dependencies
        ) as R;
        return newObj!;
    }
}