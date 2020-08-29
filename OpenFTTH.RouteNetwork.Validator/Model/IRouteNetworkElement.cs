using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator.Model
{
    public interface IRouteNetworkElement
    {
        Guid Id { get; }
        Envelope Envelope { get; }
    }
}
