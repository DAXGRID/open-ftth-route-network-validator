using DAX.ObjectVersioning.Core;
using Microsoft.Extensions.Logging;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.RouteNetwork.Validator.Model;
using OpenFTTH.RouteNetwork.Validator.State;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.Validator.Projections
{
    internal sealed class RouteNetworkProjection : ProjectionBase
    {
        private readonly ILogger<RouteNetworkProjection> _logger;
        private readonly InMemoryNetworkState _inMemoryNetworkState;

        public RouteNetworkProjection(
            ILogger<RouteNetworkProjection> logger,
            InMemoryNetworkState inMemoryNetworkState)
        {
            _logger = logger;
            _inMemoryNetworkState = inMemoryNetworkState;
            ProjectEventAsync<RouteNetworkEditOperationOccuredEvent>(Project);
        }

        private Task Project(IEventEnvelope eventEnvelope)
        {
            switch (eventEnvelope.Data)
            {
                case (RouteNetworkEditOperationOccuredEvent @event):
                    Handle(@event);
                    break;
                default:
                    throw new ArgumentException(
                        $"Could not handle type {eventEnvelope.GetType()}");
            }

            return Task.CompletedTask;
        }

        private void Handle(RouteNetworkEditOperationOccuredEvent request)
        {
            var trans = _inMemoryNetworkState.GetTransaction();

            if (request.RouteNetworkCommands != null)
            {
                foreach (var command in request.RouteNetworkCommands)
                {
                    if (command.RouteNetworkEvents != null)
                    {
                        foreach (var routeNetworkEvent in command.RouteNetworkEvents)
                        {
                            switch (routeNetworkEvent)
                            {
                                case RouteNodeAdded domainEvent:
                                    HandleEvent(domainEvent, trans);
                                    break;

                                case RouteNodeMarkedForDeletion domainEvent:
                                    HandleEvent(domainEvent, trans);
                                    break;

                                case RouteSegmentAdded domainEvent:
                                    HandleEvent(domainEvent, trans);
                                    break;

                                case RouteSegmentMarkedForDeletion domainEvent:
                                    HandleEvent(domainEvent, trans);
                                    break;

                                case RouteSegmentRemoved domainEvent:
                                    HandleEvent(domainEvent, trans);
                                    break;
                            }
                        }
                    }
                }
            }

            _inMemoryNetworkState.FinishWithTransaction();
        }

        private void HandleEvent(RouteNodeAdded request, ITransaction transaction)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event seq no: {request.EventSequenceNumber}");

            var envelope = GeoJsonConversionHelper.ConvertFromPointGeoJson(request.Geometry).Envelope.EnvelopeInternal;

            transaction.Add(new RouteNode(request.NodeId, request.RouteNodeInfo?.Function, envelope, request.NamingInfo?.Name), ignoreDublicates: true);
        }

        private void HandleEvent(RouteSegmentAdded request, ITransaction transaction)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event seq no: {request.EventSequenceNumber}");

            if (!(_inMemoryNetworkState.GetObject(request.FromNodeId) is RouteNode fromNode))
            {
                _logger.LogError($"Route network event stream seems to be broken! RouteSegmentAdded event with id: {request.EventId} and segment id: {request.SegmentId} has a FromNodeId: {request.FromNodeId} that don't exists in the current state.");
                return;
            }

            if (!(_inMemoryNetworkState.GetObject(request.ToNodeId) is RouteNode toNode))
            {
                _logger.LogError($"Route network event stream seems to be broken! RouteSegmentAdded event with id: {request.EventId} and segment id: {request.SegmentId} has a ToNodeId: {request.ToNodeId} that don't exists in the current state.");
                return;
            }

            var envelope = GeoJsonConversionHelper.ConvertFromLineGeoJson(request.Geometry).Envelope.EnvelopeInternal;

            transaction.Add(new RouteSegment(request.SegmentId, fromNode, toNode, envelope), ignoreDublicates: true);
        }

        private void HandleEvent(RouteSegmentMarkedForDeletion request, ITransaction transaction)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event seq no: {request.EventSequenceNumber}");

            transaction.Delete(request.SegmentId, ignoreDublicates: true);
        }

        private void HandleEvent(RouteSegmentRemoved request, ITransaction transaction)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event seq no: {request.EventSequenceNumber}");

            transaction.Delete(request.SegmentId, ignoreDublicates: true);
        }

        private void HandleEvent(RouteNodeMarkedForDeletion request, ITransaction transaction)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event seq no: {request.EventSequenceNumber}");

            transaction.Delete(request.NodeId, ignoreDublicates: true);
        }
    }
}
