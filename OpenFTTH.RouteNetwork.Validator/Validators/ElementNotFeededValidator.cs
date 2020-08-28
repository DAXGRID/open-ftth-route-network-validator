using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.RouteNetwork.Validator.Config;
using OpenFTTH.RouteNetwork.Validator.Database.Impl;
using OpenFTTH.RouteNetwork.Validator.Model;
using OpenFTTH.RouteNetwork.Validator.State;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator.Validators
{
    public class ElementNotFeededValidator : IValidator
    {
        private readonly ILogger _logger;
        private readonly InMemoryNetworkState _inMemoryNetworkState;
        private readonly PostgresWriter _postgresWriter;
        private readonly IOptions<DatabaseSetting> _databaseSetting;

        private List<Guid> _lastIdsNotFeeded = new List<Guid>();

        public ElementNotFeededValidator(ILogger<ElementNotFeededValidator> logger, InMemoryNetworkState inMemoryNetworkState, PostgresWriter postgresWriter, IOptions<DatabaseSetting> databaseSetting)
        {
            _logger = logger;
            _inMemoryNetworkState = inMemoryNetworkState;
            _postgresWriter = postgresWriter;
            _databaseSetting = databaseSetting;
        }

        public void CreateTable(IDbTransaction transaction)
        {
            _postgresWriter.CreateIdTable(_databaseSetting.Value.Schema, _databaseSetting.Value.ElementNotFeededTableName, transaction);
        }

        public void Validate(bool initial)
        {
            _logger.LogInformation($"{this.GetType().Name} started a network trace...");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Dictionary<Guid, RouteNode> secondaryNodeLookup = new Dictionary<Guid, RouteNode>();

            HashSet<Guid> allNetworkObjectIds = new HashSet<Guid>();
            HashSet<Guid> networkObjectsConnectedToSecondaryNodes = new HashSet<Guid>();

            foreach (var obj in _inMemoryNetworkState.ObjectManager.GetObjects())
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
                long version = _inMemoryNetworkState.ObjectManager.GetLatestCommitedVersion();

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

            _logger.LogInformation($"{this.GetType().Name} finish tracing. Elapsed time: {stopwatch.Elapsed.ToString("mm\\:ss\\.ff")}");
            _logger.LogInformation($"{this.GetType().Name} analysis result: {idsNotFeeded.Count} out of {allNetworkObjectIds.Count} route network elements were not feeded/connected to a central office.");

            stopwatch.Start();

            _logger.LogInformation($"{this.GetType().Name} writing analysis result to database started...");

            if (initial)
            {
                _postgresWriter.TruncateAndWriteGuidsToTable(_databaseSetting.Value.Schema, _databaseSetting.Value.ElementNotFeededTableName, idsNotFeeded);
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

                _postgresWriter.DeleteGuidsFromTable(_databaseSetting.Value.Schema, _databaseSetting.Value.ElementNotFeededTableName, idsToBeDeleted);
                _postgresWriter.AddGuidsToTable(_databaseSetting.Value.Schema, _databaseSetting.Value.ElementNotFeededTableName, idsToBeAdded);

                _lastIdsNotFeeded = idsNotFeeded;
            }

            stopwatch.Stop();
            _logger.LogInformation($"{this.GetType().Name} writing analysis result to database finish. Elapsed time: {stopwatch.Elapsed.ToString("mm\\:ss\\.ff")}");
        }
    }
}
