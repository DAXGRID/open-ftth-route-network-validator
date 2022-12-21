using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.Geo;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.RouteNetwork.Validator.Config;
using OpenFTTH.RouteNetwork.Validator.Database.Impl;
using OpenFTTH.RouteNetwork.Validator.Model;
using OpenFTTH.RouteNetwork.Validator.Notification;
using OpenFTTH.RouteNetwork.Validator.Producer;
using OpenFTTH.RouteNetwork.Validator.State;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;

namespace OpenFTTH.RouteNetwork.Validator.Validators
{
    public class ElementNotFeededValidator : IValidator
    {
        private readonly ILogger _logger;
        private readonly InMemoryNetworkState _inMemoryNetworkState;
        private readonly PostgresWriter _postgresWriter;
        private readonly DatabaseSetting _databaseSetting;
        private readonly IProducer _eventProducer;
        private readonly KafkaSetting _kafkaSetting;
        private readonly INotificationClient _notificationClient;

        private Dictionary<Guid, IRouteNetworkElement> _lastNetworkElementsNotFeeded = new Dictionary<Guid, IRouteNetworkElement>();

        public ElementNotFeededValidator(
            ILogger<ElementNotFeededValidator> logger,
            InMemoryNetworkState inMemoryNetworkState,
            PostgresWriter postgresWriter,
            IOptions<DatabaseSetting> databaseSetting,
            IProducer eventProducer,
            IOptions<KafkaSetting> kafkaSetting,
            INotificationClient notificationClient)
        {
            _logger = logger;
            _inMemoryNetworkState = inMemoryNetworkState;
            _postgresWriter = postgresWriter;
            _databaseSetting = databaseSetting.Value;
            _eventProducer = eventProducer;
            _kafkaSetting = kafkaSetting.Value;
            _notificationClient = notificationClient;
        }

        public void CreateTable(IDbTransaction transaction)
        {
            _postgresWriter.CreateIdTable(
                _databaseSetting.Schema, _databaseSetting.ElementNotFeededTableName, transaction);
        }

        public void Validate(bool initial, IDbTransaction trans)
        {
            _logger.LogInformation($"{this.GetType().Name} started a network trace...");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Dictionary<Guid, RouteNode> secondaryNodeLookup = new Dictionary<Guid, RouteNode>();
            Dictionary<Guid, IRouteNetworkElement> allNetworkElements = new Dictionary<Guid, IRouteNetworkElement>();
            Dictionary<Guid, IRouteNetworkElement> elementsConnectedToSecondaryNode = new Dictionary<Guid, IRouteNetworkElement>();

            foreach (var obj in _inMemoryNetworkState.ObjectManager.GetObjects())
            {
                if (obj is RouteNode)
                {
                    var routeNode = obj as RouteNode;

                    // We keep all secondary nodes (the central offices with active equipment feeding the customer) in a dict for fast lookup
                    if (routeNode.Function == RouteNodeFunctionEnum.SecondaryNode)
                        secondaryNodeLookup.Add(routeNode.Id, routeNode);
                }

                allNetworkElements.Add(obj.Id, (IRouteNetworkElement)obj);
            }

            // Now, trace every secondary nodes
            foreach (var node in secondaryNodeLookup.Values)
            {
                long version = _inMemoryNetworkState.ObjectManager.GetLatestCommitedVersion();

                var traceResult = node.UndirectionalDFS<RouteNode, RouteSegment>(version, n => !elementsConnectedToSecondaryNode.ContainsKey(n.Id));

                foreach (var obj in traceResult)
                {
                    if (!elementsConnectedToSecondaryNode.ContainsKey(obj.Id))
                        elementsConnectedToSecondaryNode.Add(obj.Id, (IRouteNetworkElement)obj);
                }
            }

            // Find all route network elements that are not reached by secondary node / central office
            Dictionary<Guid, IRouteNetworkElement> elementsNotConnectedToSecondaryNode = new Dictionary<Guid, IRouteNetworkElement>();

            foreach (var networkElement in allNetworkElements.Values)
            {
                if (!elementsConnectedToSecondaryNode.ContainsKey(networkElement.Id) && !elementsNotConnectedToSecondaryNode.ContainsKey(networkElement.Id))
                    elementsNotConnectedToSecondaryNode.Add(networkElement.Id, networkElement);
            }

            stopwatch.Stop();

            _logger.LogInformation($"{this.GetType().Name} finish tracing. Elapsed time: {stopwatch.Elapsed.Milliseconds} milliseconds.");
            _logger.LogInformation($"{this.GetType().Name} analysis result: {elementsNotConnectedToSecondaryNode.Count} out of {allNetworkElements.Count} route network elements were not feeded/connected to a central office.");

            stopwatch.Start();

            _logger.LogInformation($"{this.GetType().Name} writing analysis result to database started...");

            if (initial)
            {
                _postgresWriter.TruncateAndWriteGuidsToTable(_databaseSetting.Schema, _databaseSetting.ElementNotFeededTableName, elementsNotConnectedToSecondaryNode.Keys, trans);
                _lastNetworkElementsNotFeeded = elementsNotConnectedToSecondaryNode;
            }
            else
            {
                Dictionary<Guid, IRouteNetworkElement> elementsToBeDeleted = new Dictionary<Guid, IRouteNetworkElement>();
                Dictionary<Guid, IRouteNetworkElement> elementsToBeAdded = new Dictionary<Guid, IRouteNetworkElement>();

                // Find elements to delete
                foreach (var lastTimeNotFeededElement in _lastNetworkElementsNotFeeded.Values)
                {
                    if (!elementsNotConnectedToSecondaryNode.ContainsKey(lastTimeNotFeededElement.Id))
                        elementsToBeDeleted.Add(lastTimeNotFeededElement.Id, lastTimeNotFeededElement);
                }

                // Find elements to add
                foreach (var addedElement in elementsNotConnectedToSecondaryNode.Values)
                {
                    if (!_lastNetworkElementsNotFeeded.ContainsKey(addedElement.Id))
                        elementsToBeAdded.Add(addedElement.Id, addedElement);
                }

                if (elementsToBeAdded.Count == 0 && elementsToBeDeleted.Count == 0)
                {
                    _logger.LogInformation($"{this.GetType().Name} No change compared to last validation - regarding which route network elements are feeded or not. Will therefore not send ObjectsWithinGeographicalAreaUpdated event or update database.");
                }
                else
                {
                    // We just truncate and write all the ids to the table, because it's faster than doing individual add and delete stmts
                    _postgresWriter.TruncateAndWriteGuidsToTable(_databaseSetting.Schema, _databaseSetting.ElementNotFeededTableName, elementsNotConnectedToSecondaryNode.Keys, trans);

                    // Publish an event telling what elements that have been affected by validation
                    PublishObjectsWithinGeographicalAreaUpdatedEvent(elementsToBeAdded, elementsToBeDeleted);
                }

                _lastNetworkElementsNotFeeded = elementsNotConnectedToSecondaryNode;
            }

            stopwatch.Stop();
            _logger.LogInformation($"{this.GetType().Name} writing analysis result to database finish. Elapsed time: {stopwatch.Elapsed.Milliseconds} milliseconds.");
        }

        private void PublishObjectsWithinGeographicalAreaUpdatedEvent(
            Dictionary<Guid, IRouteNetworkElement> addedElements,
            Dictionary<Guid, IRouteNetworkElement> deletedElements)
        {
            List<IdChangeSet> idChangeSets = new List<IdChangeSet>();

            // Only add ids, if less that 1000. This to prevent message size to big error in Kafka
            if ((addedElements.Count + deletedElements.Count <= 1000))
            {
                // NB: We don't have to deal with modifications, because the element not feeded validation just produces a list of route network element ids that are not feeded
                if (addedElements.Count > 0)
                    idChangeSets.Add(new IdChangeSet("RouteElementNotFeeded", ChangeTypeEnum.Addition, addedElements.Keys.ToArray()));

                if (deletedElements.Count > 0)
                    idChangeSets.Add(new IdChangeSet("RouteElementNotFeeded", ChangeTypeEnum.Deletion, deletedElements.Keys.ToArray()));
            }

            // Create an envelop that covers all route network elements
            // that have either added og deleted by the validation logic
            var env = new Envelope();

            foreach (var addedElement in addedElements.Values)
            {
                env.ExpandToInclude(addedElement.Envelope);
            }

            foreach (var deletedElement in deletedElements.Values)
            {
                env.ExpandToInclude(deletedElement.Envelope);
            }

            var envelopeInfo = new EnvelopeInfo(env.MinX, env.MaxX, env.MinY, env.MaxY);

            var graphicalObjectsUpdatedEvent =
                new ObjectsWithinGeographicalAreaUpdated(
                    eventType: typeof(ObjectsWithinGeographicalAreaUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "RouteNetworkValidator",
                    applicationInfo: null,
                    category: "RouteNetworkValidation",
                    envelope: envelopeInfo,
                    idChangeSets: idChangeSets.ToArray()
                );

            _notificationClient.Notify(
                "GeographicalAreaUpdated",
                JsonConvert.SerializeObject(graphicalObjectsUpdatedEvent));
        }
    }
}
