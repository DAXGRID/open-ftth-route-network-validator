namespace OpenFTTH.RouteNetwork.Validator.Config;

internal sealed record EventStoreSetting
{
    public string ConnectionString { get; init; }
}
