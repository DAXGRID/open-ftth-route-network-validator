namespace OpenFTTH.RouteNetwork.Validator.Config;

internal sealed record NotificationServerSetting
{
    public string Domain { get; init; }
    public int Port { get; init; }
}
