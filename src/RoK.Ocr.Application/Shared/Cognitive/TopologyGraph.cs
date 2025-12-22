using RoK.Ocr.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using System; // Added System to ensure Math is available if not globally imported

namespace RoK.Ocr.Application.Shared.Cognitive;

// Defining the Enum here or in a separate file in Shared
public enum Direction { Up, Down, Left, Right }

public class TopologyGraph
{
    private readonly List<AnalyzedBlock> _nodes;
    private readonly double _canvasWidth;
    private readonly double _canvasHeight;

    public TopologyGraph(List<AnalyzedBlock> nodes, double width, double height)
    {
        _nodes = nodes;
        _canvasWidth = width;
        _canvasHeight = height;
    }

    // Ensure it returns List<AnalyzedBlock> and not void!
    public List<AnalyzedBlock> GetNodesInRegion(double minX, double maxX, double minY, double maxY)
    {
        return _nodes.Where(n =>
            (n.Raw.Box[0][0] / _canvasWidth) >= minX &&
            (n.Raw.Box[0][0] / _canvasWidth) <= maxX &&
            (n.Raw.Box[0][1] / _canvasHeight) >= minY &&
            (n.Raw.Box[0][1] / _canvasHeight) <= maxY).ToList();
    }

    public AnalyzedBlock? FindNeighbor(AnalyzedBlock source, Direction direction, double maxDistancePercent = 0.3)
    {
        var (srcX, srcY) = source.NormalizedCenter;

        return _nodes
            .Where(n => n != source)
            .Select(n => new { Node = n, Dist = CalculateWeightedDistance(srcX, srcY, n, direction) })
            .Where(x => x.Dist < 1000) // Filters invalid candidates (MaxValue)
            .OrderBy(x => x.Dist)
            .Select(x => x.Node)
            .FirstOrDefault(n =>
                Math.Abs(n.NormalizedCenter.X - srcX) < maxDistancePercent &&
                Math.Abs(n.NormalizedCenter.Y - srcY) < maxDistancePercent);
    }

    private double CalculateWeightedDistance(double srcX, double srcY, AnalyzedBlock target, Direction dir)
    {
        var (tarX, tarY) = target.NormalizedCenter;
        double dx = tarX - srcX;
        double dy = tarY - srcY;

        return dir switch
        {
            // For right/left: we heavily penalize vertical deviation (dy * 10)
            Direction.Right => (dx > 0) ? (Math.Abs(dy) * 10 + dx) : double.MaxValue,
            Direction.Left => (dx < 0) ? (Math.Abs(dy) * 10 + Math.Abs(dx)) : double.MaxValue,

            // For up/down: we heavily penalize horizontal deviation (dx * 10)
            Direction.Down => (dy > 0) ? (Math.Abs(dx) * 10 + dy) : double.MaxValue,
            Direction.Up => (dy < 0) ? (Math.Abs(dx) * 10 + Math.Abs(dy)) : double.MaxValue,

            _ => double.MaxValue
        };
    }
}