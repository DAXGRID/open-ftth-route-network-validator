using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFTTH.RouteNetwork.Validator.Config;
using System;
using System.Collections.Generic;
using System.Text;
using Topos.Config;
using OpenFTTH.Events;
using MediatR;
using DAX.EventProcessing.Serialization;

namespace OpenFTTH.RouteNetwork.Validator.Consumers.Kafka
{
    public class KafkaEventDispatcher : IGenericEventDispatcher
    {
        private IDisposable _consumer;
        private readonly ILogger<KafkaEventDispatcher> _logger;
        private readonly IMediator _mediator;
        private readonly KafkaSetting _kafkaSetting;

        public KafkaEventDispatcher(
            ILogger<KafkaEventDispatcher> logger,
            IMediator mediator,
            IOptions<KafkaSetting> kafkaSetting
            )
        {
            _logger = logger;
            _mediator = mediator;
            _kafkaSetting = kafkaSetting.Value;
        }

        public void Start()
        {
            ConfigAndStart();
        }

        private void ConfigAndStart()
        {
            _consumer = Configure
                          .Consumer(_kafkaSetting.DatafordelerTopic, c => c.UseKafka(_kafkaSetting.Server))
                          .Logging(l => l.UseSerilog())
                          .Serialization(s => s.GenericEventDeserializer<IDomainEvent>())
                          .Topics(t => t.Subscribe(_kafkaSetting.DatafordelerTopic))
                          .Positions(p => p.StoreInFileSystem(_kafkaSetting.PositionFilePath))
                          .Handle(async (messages, context, token) =>
                          {
                              foreach (var message in messages)
                              {
                                  switch (message.Body)
                                  {
                           // We received an event that a class is defined for, so it should be handled by someone
                           case IDomainEvent domainEvent:
                                          _logger.LogDebug($"The received Kafka event: {nameof(domainEvent)} is send to handler...");
                                          await _mediator.Send(domainEvent);
                                          break;

                           // We received an event that could not be deserialized
                           case EventCouldNotBeDeserialized unhandledEvent:
                                          _logger.LogDebug($"The received Kafka event: {unhandledEvent.EventClassName} could not be deserialized and therefore not dispatched to handler. {unhandledEvent.ErrorMessage}");
                                          break;
                                  }
                              }
                          }).Start();
        }

        public void Dispose()
        {
        }
    }
}
