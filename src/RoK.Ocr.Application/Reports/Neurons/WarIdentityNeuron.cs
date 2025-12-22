using System;
using System.Collections.Generic;
using System.Linq;
using RoK.Ocr.Domain.Enums;
using RoK.Ocr.Domain.Models;
using RoK.Ocr.Application.Shared.Cognitive; // IMPORTANT: To find TopologyGraph and Direction

namespace RoK.Ocr.Application.Reports.Neurons;

public class WarIdentityNeuron
{
    public (string Name, string Tag) Extract(TopologyGraph graph, SideLocation side)
    {
        double minX = side == SideLocation.Attacker ? 0.0 : 0.5;
        double maxX = side == SideLocation.Attacker ? 0.5 : 1.0;

        // Now GetNodesInRegion returns List, so FirstOrDefault works
        var nodes = graph.GetNodesInRegion(minX, maxX, 0.05, 0.4);

        var tagNode = nodes.FirstOrDefault(n => n.Type == BlockType.Tag);

        if (tagNode == null)
        {
            var unitsLabel = nodes.FirstOrDefault(n => n.Type == BlockType.UnitsLabel);
            if (unitsLabel != null)
            {
                var nameCandidate = graph.FindNeighbor(unitsLabel, Direction.Up, 0.2);
                if (nameCandidate != null) return (nameCandidate.Raw.Text, "--");
            }
        }

        return ("--", "--");
    }
}

public enum SideLocation { Attacker, Defender }