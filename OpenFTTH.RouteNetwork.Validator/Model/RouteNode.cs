using DAX.ObjectVersioning.Graph;
using NetTopologySuite.Geometries;
using OpenFTTH.Events.RouteNetwork.Infos;
using System;

namespace OpenFTTH.RouteNetwork.Validator.Model;

public class RouteNode : GraphNode, IRouteNetworkElement
{
    private readonly RouteNodeFunctionEnum? _function;
    private readonly string _name;
    private readonly Envelope _envelope;

    public RouteNodeFunctionEnum? Function => _function;
    public string Name => _name;
    public Envelope Envelope => _envelope;

    public RouteNode(Guid id, RouteNodeFunctionEnum? function, Envelope envelope, string name = null) : base(id)
    {
        _function = function;
        _envelope = envelope;
        _name = name;
    }
}
