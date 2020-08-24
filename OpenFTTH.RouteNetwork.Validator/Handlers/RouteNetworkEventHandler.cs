using DAX.ObjectVersioning.Core;
using DAX.ObjectVersioning.Graph;
using MediatR;
using Microsoft.Extensions.Logging;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.RouteNetwork.Validator.Model;
using OpenFTTH.RouteNetwork.Validator.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.Validator.Handlers
{
    public class RouteNetworkEventHandler :
        IRequestHandler<RouteNodeAdded>,
        IRequestHandler<RouteSegmentAdded>,
        IRequestHandler<RouteNodeMarkedForDeletion>,
        IRequestHandler<RouteSegmentMarkedForDeletion>,
        IRequestHandler<RouteNodeGeometryModified>,
        IRequestHandler<RouteSegmentGeometryModified>
    {
        private readonly ILogger<RouteNetworkEventHandler> _logger;

        private InMemoryNetworkState _inMemoryNetworkState;

        public RouteNetworkEventHandler(ILogger<RouteNetworkEventHandler> logger, InMemoryNetworkState inMemoryNetworkState)
        {
            _logger = logger;
            _inMemoryNetworkState = inMemoryNetworkState;
        }


        public Task<Unit> Handle(RouteNodeAdded request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event.");

            var trans = _inMemoryNetworkState.GetTransaction();

            trans.Add(new RouteNode(request.NodeId, request.RouteNodeInfo?.Function, request.NamingInfo?.Name));

            _inMemoryNetworkState.FinishWithTransaction(request.IsLastEventInCmd);

            return Unit.Task;
        }

        public Task<Unit> Handle(RouteSegmentAdded request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event.");

            var trans = _inMemoryNetworkState.GetTransaction();

            var fromNode = _inMemoryNetworkState.GetObject(request.FromNodeId) as RouteNode;

            if (fromNode == null)
                throw new DataMisalignedException($"Route network event stream seemd to be broken! RouteSegmentAdded event with id: {request.EventId} has a FromNodeId: {request.FromNodeId} that don't exists in the current state.");

            var toNode = _inMemoryNetworkState.GetObject(request.ToNodeId) as RouteNode;

            if (toNode == null)
                throw new DataMisalignedException($"Route network event stream seemd to be broken! RouteSegmentAdded event with id: {request.EventId} has a ToNodeId: {request.ToNodeId} that don't exists in the current state.");

            trans.Add(new RouteSegment(request.SegmentId, fromNode, toNode));

            _inMemoryNetworkState.FinishWithTransaction(request.IsLastEventInCmd);

            return Unit.Task;
        }

        public Task<Unit> Handle(RouteSegmentMarkedForDeletion request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Unit> Handle(RouteNodeMarkedForDeletion request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Unit> Handle(RouteNodeGeometryModified request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Unit> Handle(RouteSegmentGeometryModified request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
