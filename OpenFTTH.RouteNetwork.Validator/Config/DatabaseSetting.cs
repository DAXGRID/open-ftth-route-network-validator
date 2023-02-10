namespace OpenFTTH.RouteNetwork.Validator.Config;

internal sealed record DatabaseSetting
{
    public string Host { get; set; }
    public string Port { get; set; }
    public string Database { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Schema { get; set; }
    public string ElementNotFeededTableName { get; set; }
}
