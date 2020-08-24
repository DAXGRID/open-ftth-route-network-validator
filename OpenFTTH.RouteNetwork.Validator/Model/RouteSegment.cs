using DAX.ObjectVersioning.Graph;
using OpenFTTH.Events.RouteNetwork.Infos;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator.Model
{
    public class RouteSegment : GraphEdge
    {
        public RouteSegment(Guid id, RouteNode fromNode, RouteNode toNode) : base(id, fromNode, toNode)
        {
        }
    }
}
