using DAX.ObjectVersioning.Graph;
using NetTopologySuite.Geometries;
using System;

namespace OpenFTTH.RouteNetwork.Validator.Model;

public class RouteSegment : GraphEdge, IRouteNetworkElement
{
    private readonly Envelope _envelope;
    public Envelope Envelope => _envelope;

    public RouteSegment(Guid id, RouteNode fromNode, RouteNode toNode, Envelope envelope) : base(id, fromNode, toNode)
    {
        _envelope = envelope;
    }
}
