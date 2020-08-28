using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator.Config
{
    public class DatabaseSetting
    {
        public string Host { get; set; }
        public string Port { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Schema { get; set; }
        public string ElementNotFeededTableName { get; set; }
    }
}
