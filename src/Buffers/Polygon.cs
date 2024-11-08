/* Author:  Leonardo Trevisan Silio
 * Date:    06/11/2024
 */
namespace Radiance.Buffers;

using Internal;

/// <summary>
/// A buffer with a Polygon points.
/// </summary>
public class Polygon : IPolygon
{
    readonly float[] data;
    public Polygon(float[] data)
    {
        this.data = data;
        Buffer = Buffer.From(this);
    }
    private Vec3Buffer? triangulationPair = null;

    /// <summary>
    /// Get the triangulation of this polygon.
    /// </summary>
    public Vec3Buffer Triangules
        => triangulationPair ??= FindTriangules();

    Vec3Buffer FindTriangules()
    {   
        var triangules = Triangulations
            .PlanarPolygonTriangulation(data[..]);
        
        return new(triangules);
    }

    public float[] GetBufferData()
        => data[..];
        
    public Buffer Buffer { get; private set; }
    
    public int Count => data.Length / 3;
    public int Size => 3;

    public static implicit operator Polygon(float[] data) => new(data);

    public static RepeatPolygon operator *(Polygon polygon, int times)
        => new (polygon, times);

    public static RepeatPolygon operator *(int times, Polygon polygon)
        => new (polygon, times);
}