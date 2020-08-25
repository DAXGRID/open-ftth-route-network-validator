using DAX.ObjectVersioning.Core;
using Microsoft.Extensions.Logging;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.RouteNetwork.Validator.Database.Impl;
using OpenFTTH.RouteNetwork.Validator.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator.State
{
    public class InMemoryNetworkState
    {
        private string _routeValidationNotFeededTableName = "route_network_validator.element_not_feeded";

        private readonly ILogger<InMemoryNetworkState> _logger;

        private readonly PostgressWriter _postgresWriter;

        private InMemoryObjectManager _objectManager = new InMemoryObjectManager();

        private bool _loadMode = true;

        private ITransaction _loadModeTransaction;

        private ITransaction _cmdTransaction;

        private DateTime __lastEventRecievedTimestamp = DateTime.UtcNow;
        public DateTime LastEventRecievedTimestamp => __lastEventRecievedTimestamp;

        private List<Guid> _lastIdsNotFeeded = new List<Guid>();

        public InMemoryNetworkState(ILogger<InMemoryNetworkState> logger, PostgressWriter postgresWriter)
        {
            _logger = logger;
            _postgresWriter = postgresWriter;
        }

        public ITransaction GetTransaction()
        {
            if (_loadMode)
                return GetLoadModeTransaction();
            else
                return GetCommandTransaction();
        }

        public void FinishWithTransaction(bool lastEventInCommand)
        {
            __lastEventRecievedTimestamp = DateTime.UtcNow;

            // We're our of load mode, and dealing with last event
            if (!_loadMode && _loadModeTransaction == null && lastEventInCommand)
            {
                // Commit the command transaction
                _cmdTransaction.Commit();
                _cmdTransaction = null;

                DoTrace();
            }
        }

        public IVersionedObject GetObject(Guid id)
        {
            if (_loadMode && _loadModeTransaction != null)
                return _loadModeTransaction.GetObject(id);
            else if (_cmdTransaction != null)
            {
                var transObj = _cmdTransaction.GetObject(id);

                if (transObj != null)
                    return transObj;
                else
                    return _objectManager.GetObject(id);
            }
            else
                return null;
        }

        private void DoTrace(bool initial = false)
        {
            _logger.LogInformation("Validating processing/tracing started...");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Dictionary<Guid, RouteNode> secondaryNodeLookup = new Dictionary<Guid, RouteNode>();

            HashSet<Guid> allNetworkObjectIds = new HashSet<Guid>();
            HashSet<Guid> networkObjectsConnectedToSecondaryNodes = new HashSet<Guid>();

            foreach (var obj in _objectManager.GetObjects())
            {
                if (obj is RouteNode)
                {
                    var routeNode = obj as RouteNode;

                    // We keep all secondary nodes (the central offices with active equipment feeding the customer) in a dict for fast lookup
                    if (routeNode.Function == RouteNodeFunctionEnum.SecondaryNode)
                        secondaryNodeLookup.Add(routeNode.Id, routeNode);
                }

                allNetworkObjectIds.Add(obj.Id);
            }

            // Now, trace every secondary nodes

            foreach (var node in secondaryNodeLookup.Values)
            {
                long version = _objectManager.GetLatestCommitedVersion();

                var traceResult = node.UndirectionalDFS<RouteNode, RouteSegment>(version, n => !networkObjectsConnectedToSecondaryNodes.Contains(n.Id));

                foreach (var obj in traceResult)
                    networkObjectsConnectedToSecondaryNodes.Add(obj.Id);
            }

            // Get list of all the objects that are not reached by secondary node
            List<Guid> idsNotFeeded = new List<Guid>();

            foreach (var networkObjectId in allNetworkObjectIds)
            {
                if (!networkObjectsConnectedToSecondaryNodes.Contains(networkObjectId) && !idsNotFeeded.Contains(networkObjectId))
                    idsNotFeeded.Add(networkObjectId);
            }

            stopwatch.Stop();
            
            _logger.LogInformation($"Validating processing/tracing finish. Elapsed time: {stopwatch.Elapsed.ToString("mm\\:ss\\.ff")}");
            _logger.LogInformation($"Analysis result: {idsNotFeeded.Count} out of {allNetworkObjectIds.Count} route network elements were not feeded/connected to a central office.");

            stopwatch.Start();

            _logger.LogInformation($"Writing analysis result to database started...");
            if (initial)
            {
                _postgresWriter.TruncateAndWriteGuidsToTable(_routeValidationNotFeededTableName, idsNotFeeded);
                _lastIdsNotFeeded = idsNotFeeded;
            }
            else
            {
                List<Guid> idsToBeDeleted = new List<Guid>();
                List<Guid> idsToBeAdded = new List<Guid>();

                // Find ids to delete
                foreach (var lastId in _lastIdsNotFeeded)
                {
                    if (!idsNotFeeded.Contains(lastId))
                        idsToBeDeleted.Add(lastId);
                }

                // Find ids to add
                foreach (var id in idsNotFeeded)
                {
                    if (!_lastIdsNotFeeded.Contains(id))
                        idsToBeAdded.Add(id);
                }

                _postgresWriter.DeleteGuidsFromTable(_routeValidationNotFeededTableName, idsToBeDeleted);
                _postgresWriter.AddGuidsToTable(_routeValidationNotFeededTableName, idsToBeAdded);

                _lastIdsNotFeeded = idsNotFeeded;
            }

            stopwatch.Stop();
            _logger.LogInformation($"Writing analysis result to database finish. Elapsed time: {stopwatch.Elapsed.ToString("mm\\:ss\\.ff")}");


        }


        public void FinishLoadMode()
        {
            _loadMode = false;
            _loadModeTransaction.Commit();
            _loadModeTransaction = null;

            DoTrace(true);
        }

        private ITransaction GetLoadModeTransaction()
        {
            if (_loadModeTransaction == null)
                _loadModeTransaction = _objectManager.CreateTransaction();

            return _loadModeTransaction;
        }

        private ITransaction GetCommandTransaction()
        {
            if (_cmdTransaction == null)
                _cmdTransaction = _objectManager.CreateTransaction();

            return _cmdTransaction;
        }
    }
}
