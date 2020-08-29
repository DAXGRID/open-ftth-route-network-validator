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
        IRequestHandler<RouteSegmentRemoved>,
        IRequestHandler<RouteNodeGeometryModified>,
        IRequestHandler<RouteSegmentGeometryModified>
    {
        private readonly ILogger<RouteNetworkEventHandler> _logger;

        private InMemoryNetworkState _inMemoryNetworkState;

        private HashSet<Guid> _alreadyProcessed = new HashSet<Guid>();

        public RouteNetworkEventHandler(ILogger<RouteNetworkEventHandler> logger, InMemoryNetworkState inMemoryNetworkState)
        {
            _logger = logger;
            _inMemoryNetworkState = inMemoryNetworkState;
        }


        public Task<Unit> Handle(RouteNodeAdded request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event seq no: {request.EventSequenceNumber}");

            if (AlreadyProcessed(request.EventId))
                return Unit.Task;

            var trans = _inMemoryNetworkState.GetTransaction();

            var envelope = GeoJsonConversionHelper.ConvertFromPointGeoJson(request.Geometry).Envelope.EnvelopeInternal;

            trans.Add(new RouteNode(request.NodeId, request.RouteNodeInfo?.Function, envelope, request.NamingInfo?.Name));

            _inMemoryNetworkState.FinishWithTransaction(request.IsLastEventInCmd);

            return Unit.Task;
        }

        public Task<Unit> Handle(RouteSegmentAdded request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event seq no: {request.EventSequenceNumber}");

            if (AlreadyProcessed(request.EventId))
                return Unit.Task;

            var trans = _inMemoryNetworkState.GetTransaction();

            var fromNode = _inMemoryNetworkState.GetObject(request.FromNodeId) as RouteNode;

            if (fromNode == null)
                throw new DataMisalignedException($"Route network event stream seemd to be broken! RouteSegmentAdded event with id: {request.EventId} has a FromNodeId: {request.FromNodeId} that don't exists in the current state.");

            var toNode = _inMemoryNetworkState.GetObject(request.ToNodeId) as RouteNode;

            if (toNode == null)
                throw new DataMisalignedException($"Route network event stream seemd to be broken! RouteSegmentAdded event with id: {request.EventId} has a ToNodeId: {request.ToNodeId} that don't exists in the current state.");

            var envelope = GeoJsonConversionHelper.ConvertFromLineGeoJson(request.Geometry).Envelope.EnvelopeInternal;

            trans.Add(new RouteSegment(request.SegmentId, fromNode, toNode, envelope));

            _inMemoryNetworkState.FinishWithTransaction(request.IsLastEventInCmd);

            return Unit.Task;
        }

        public Task<Unit> Handle(RouteSegmentMarkedForDeletion request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event seq no: {request.EventSequenceNumber}");

            if (AlreadyProcessed(request.EventId))
                return Unit.Task;

            var trans = _inMemoryNetworkState.GetTransaction();

            trans.Delete(request.SegmentId);

            _inMemoryNetworkState.FinishWithTransaction(request.IsLastEventInCmd);

            return Unit.Task;
        }

        public Task<Unit> Handle(RouteSegmentRemoved request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event seq no: {request.EventSequenceNumber}");

            if (AlreadyProcessed(request.EventId))
                return Unit.Task;

            var trans = _inMemoryNetworkState.GetTransaction();

            trans.Delete(request.SegmentId);

            _inMemoryNetworkState.FinishWithTransaction(request.IsLastEventInCmd);

            return Unit.Task;
        }


        public Task<Unit> Handle(RouteNodeMarkedForDeletion request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event seq no: {request.EventSequenceNumber}");

            if (AlreadyProcessed(request.EventId))
                return Unit.Task;

            var trans = _inMemoryNetworkState.GetTransaction();

            trans.Delete(request.NodeId);

            _inMemoryNetworkState.FinishWithTransaction(request.IsLastEventInCmd);

            return Unit.Task;
        }

        public Task<Unit> Handle(RouteNodeGeometryModified request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        public Task<Unit> Handle(RouteSegmentGeometryModified request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }


        private bool AlreadyProcessed(Guid id)
        {
            if (_alreadyProcessed.Contains(id))
                return true;
            else
                return false;
        }
    }
}
