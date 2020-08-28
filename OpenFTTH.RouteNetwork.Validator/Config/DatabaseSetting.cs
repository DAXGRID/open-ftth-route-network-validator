using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator.Config
{
    public class DatabaseSetting
    {
        public string ConnectionString { get; set; }
        public string Schema { get; set; }
        public string ElementNotFeededTableName { get; set; }
    }
}
