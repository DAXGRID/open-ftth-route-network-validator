using NetTopologySuite.Geometries;
using System;

namespace OpenFTTH.RouteNetwork.Validator.Model;

public interface IRouteNetworkElement
{
    Guid Id { get; }
    Envelope Envelope { get; }
}
