using MediatR;
using Microsoft.Extensions.Logging;
using OpenFTTH.Events.RouteNetwork;
using System;
using System.Collections.Generic;
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

        public RouteNetworkEventHandler(ILogger<RouteNetworkEventHandler> logger)
        {
            _logger = logger;
        }


        public Task<Unit> Handle(RouteNodeAdded request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handler got {request.GetType().Name} event.");



            return Unit.Task;
        }

        public Task<Unit> Handle(RouteSegmentAdded request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
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
