/* Author:  Leonardo Trevisan Silio
 * Date:    29/12/2024
 */
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL;

namespace Radiance.Internal;

/// <summary>
/// A class that contains some util and opeartions.
/// </summary>
public static class Triangulations
{
    /// <summary>
    /// Get a triangulation of a polygon with points in a
    /// clockwise order.
    /// </summary>
    public static float[] PlanarPolygonTriangulation(float[] pts)
    {
        var N = pts.Length / 3;
        if (N < 4)
            return pts;

        Span<PlanarVertex> points = 
            N < 2048 ?
            stackalloc PlanarVertex[N] :
            new PlanarVertex[N];
        PlanarVertex.ToPlanarVertex(pts, points);
        
        Span<int> map =
            N < 2048 ?
            stackalloc int[N] :
            new int[N];
        var sweepLine = SweepLine.Create(points, map);

        var dcel = new DCEL(points);

        if (MonotoneDivision(dcel, sweepLine))
            return NonMonotonePlaneTriangularization(dcel, sweepLine);

        return MonotonePlaneTriangulation(dcel, sweepLine);
    }
    
    /// <summary>
    /// Divide a polygon on many monotone polygons.
    /// Return true if some polygon has created.
    /// </summary>
    static bool MonotoneDivision(DCEL dcel, SweepLine sweepLine)
    {
        var types = new VertexType[sweepLine.Length];
        for (int i = 0; i < sweepLine.Length; i++)
            types[i] = dcel.DiscoverType(i);
        
        if (!types.Contains(VertexType.Merge) && !types.Contains(VertexType.Split))
            return false;
        
        // information if polygon lies right or left te vertex
        int start = sweepLine[0].Id;
        int index = start + sweepLine.Length;
        var polygonRight = new bool[sweepLine.Length];
        polygonRight[(index - 1) % sweepLine.Length] = true;
        for (int i = 0; i < sweepLine.Length; i++, index++)
        {
            int crr = index % sweepLine.Length;
            int prev = (index - 1) % sweepLine.Length;
            polygonRight[crr] = polygonRight[prev];

            if (types[crr] == VertexType.Regular)
                continue;
            
            polygonRight[crr] = !polygonRight[crr];
        }

        HashSet<int> edgesCollect = [];
        Dictionary<int, int> helper = [];
        for (int i = 0; i < sweepLine.Length; i++)
            helper[i] = -1;
        
        for (int i = 0; i < sweepLine.Length; i++)
        {
            ref var v = ref sweepLine[i];
            var vi = v.Id;
            
            var type = types[vi];
            var edges = dcel.Edges[vi];
            var ei = edges[0].Id;
            var eprev = ei - 1;
            if (eprev == -1)
                eprev = sweepLine.Length - 1;

            switch (type)
            {
                case VertexType.Start:

                    edgesCollect.Add(ei);
                    helper[ei] = vi;

                    break;
                    
                case VertexType.End:

                    edgesCollect.Remove(eprev);

                    if (helper[eprev] == -1)
                        break;
                    
                    if (types[helper[eprev]] != VertexType.Merge)
                        break;
                    
                    dcel.Connect(vi, helper[eprev]);

                    break;

                case VertexType.Split:

                    var ej1 = dcel.FindLeftEdge(vi);
                    dcel.Connect(helper[ej1], vi);
                    helper[ej1] = vi;
                    edgesCollect.Add(ej1);

                    break;

                case VertexType.Merge:

                    if (helper[eprev] != -1 && types[helper[eprev]] == VertexType.Merge)
                    {
                        dcel.Connect(vi, helper[eprev]);
                    }

                    edgesCollect.Remove(eprev);

                    var ej2 = dcel.FindLeftEdge(vi);
                    if (helper[ej2] != -1 && types[helper[ej2]] == VertexType.Merge)
                    {
                        dcel.Connect(helper[ej2], vi);
                    }
                    helper[ej2] = vi;

                    break;

                case VertexType.Regular:

                    if (polygonRight[vi])
                    {
                        if (helper[eprev] != -1 && types[helper[eprev]] == VertexType.Merge)
                        {
                            dcel.Connect(vi, helper[eprev]);
                        }

                        edgesCollect.Remove(eprev);
                        edgesCollect.Add(ei);
                        helper[ei] = vi;
                        break;
                    }

                    var ej3 = dcel.FindLeftEdge(vi);
                    if (helper[ej3] != -1 && types[helper[ej3]] == VertexType.Merge)
                    {
                        dcel.Connect(helper[ej3], vi);
                    }
                    helper[ej3] = vi;
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Get a nonmonotone DCEL divide inot monotone polygons and returns
    /// the triangularization.
    /// </summary>
    static float[] NonMonotonePlaneTriangularization(DCEL dcel, SweepLine sweepLine)
    {
        var index = 0;
        int expectedTriangules = dcel.Vertexes.Length - 2;
        var triangules = new float[9 * expectedTriangules];

        float[] data;
        while (dcel.Faces.Count > 0)
        {
            var subDcel = dcel.RemoveSubPolygon();
            if (subDcel.Vertexes.Length < 4)
            {
                data = subDcel.ToArray();
                Array.Copy(data, 0, triangules, index, data.Length);
                index += data.Length;
                continue;
            }
            
            var subSweepLine = SweepLine.Create(subDcel.Vertexes, sweepLine.MapBuffer);
            data = MonotonePlaneTriangulation(subDcel, subSweepLine);
            Array.Copy(data, 0, triangules, index, data.Length);
            index += data.Length;
        }

        return triangules;
    }

    /// <summary>
    /// Receveing a map of ordenation and data with format (x, y, z, ...),
    /// if the points represetns a monotone polygon, return the triangularization
    /// of then.
    /// </summary>
    static float[] MonotonePlaneTriangulation(DCEL dcel, SweepLine sweepLine)
    {
        var index = 0;
        int expectedTriangules = dcel.Vertexes.Length - 2;
        var triangules = new float[9 * expectedTriangules];

        var stack = new Stack<(int id, bool chain)>();
        stack.Push((sweepLine[0].Id, false));
        stack.Push((sweepLine[1].Id, true));

        for (int k = 2; k < dcel.Vertexes.Length; k++)
        {
            ref var crrIndex = ref sweepLine[k];
            var last = stack.Pop();
            var isConn = dcel.IsConnected(last.id, crrIndex.Id);
            (int id, bool chain) mid, next = (crrIndex.Id, !(isConn ^ last.chain));
            
            if (isConn)
            {
                while (true)
                {
                    if (stack.Count == 0)
                    {
                        stack.Push(last);
                        stack.Push(next);
                        break;
                    }
                    
                    mid = last;
                    last = stack.Pop();
                    
                    if (dcel.Left(last.id, mid.id, next.id) < 0)
                    {
                        stack.Push(last);
                        stack.Push(mid);
                        stack.Push(next);
                        break;
                    }
                    
                    dcel.Connect(last.id, next.id);
                    addTriangule(
                        dcel.FindById(last.id),
                        dcel.FindById(mid.id),
                        dcel.FindById(next.id)
                    );
                }

                continue;
            }
            
            var top = last;
            mid = stack.Pop();
            dcel.Connect(last.id, next.id);
            addTriangule(
                dcel.FindById(last.id),
                dcel.FindById(mid.id),
                dcel.FindById(next.id)
            );

            while (stack.Count > 0)
            {
                last = mid;
                mid = stack.Pop();
                dcel.Connect(last.id, next.id);
                addTriangule(
                    dcel.FindById(last.id),
                    dcel.FindById(mid.id),
                    dcel.FindById(next.id)
                );
            }
            stack.Push(top);
            stack.Push(next);
        }

        if (stack.Count > 2)
        {
            addTriangule(
                dcel.FindById(stack.Pop().id),
                dcel.FindById(stack.Pop().id),
                dcel.FindById(stack.Pop().id)
            );
        }

        return triangules;

        /// <summary>
        /// Add trinagule (p, q, r) to list of triangules data
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void addTriangule(PlanarVertex p, PlanarVertex q, PlanarVertex r)
        {
            triangules[index++] = p.X;
            triangules[index++] = p.Y;
            triangules[index++] = p.Z;
            
            triangules[index++] = q.X;
            triangules[index++] = q.Y;
            triangules[index++] = q.Z;
            
            triangules[index++] = r.X;
            triangules[index++] = r.Y;
            triangules[index++] = r.Z;
        }
    }
}