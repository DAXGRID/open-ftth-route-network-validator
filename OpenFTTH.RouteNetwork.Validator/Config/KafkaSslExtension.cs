using Confluent.Kafka;
using Topos.Config;

namespace OpenFTTH.RouteNetwork.Validator.Config;

public static class KafkaSslExtension
{
    public static KafkaConsumerConfigurationBuilder WithCertificate(this KafkaConsumerConfigurationBuilder builder, string sslCaLocation)
    {
        KafkaConsumerConfigurationBuilder.AddCustomizer(builder, config =>
        {
            config.SecurityProtocol = SecurityProtocol.Ssl;
            config.SslCaLocation = sslCaLocation;
            return config;
        });
        return builder;
    }
}
