namespace OpenFTTH.RouteNetwork.Validator.Config;

public class KafkaSetting
{
    public string Server { get; set; }
    public string PositionFilePath { get; set; }
    public string RouteNetworkEventTopic { get; set; }
    public string CertificateFilename { get; set; }
}
