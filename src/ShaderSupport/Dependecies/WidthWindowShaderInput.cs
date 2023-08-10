/* Author:  Leonardo Trevisan Silio
 * Date:    10/08/2023
 */
namespace Radiance.ShaderSupport.Dependencies;

/// <summary>
/// Represents a input for the width of
/// screen used in shader implementation.
/// </summary>
public class WidthWindowShaderInput : ShaderInput
{
    public WidthWindowShaderInput()
    {
        this.Name = "width";
        this.Type = ShaderType.Float;
        this.DependenceType = ShaderDependenceType.Uniform;
    }

    public override object Value
        => Window.Width;
}