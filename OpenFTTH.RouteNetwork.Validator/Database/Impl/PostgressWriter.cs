using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenFTTH.RouteNetwork.Validator.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator.Database.Impl
{
    public class PostgressWriter
    {
        public readonly ILogger<PostgressWriter> _logger;
        public readonly IOptions<DatabaseSetting> _databaseSetting;

        public PostgressWriter(ILogger<PostgressWriter> logger, IOptions<DatabaseSetting> databaseSetting)
        {
            _logger = logger;
            _databaseSetting = databaseSetting;
        }


        public void TruncateAndWriteGuidsToTable(string tableName, IEnumerable<Guid> guids)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Truncate the table
                using (var truncateCmd = new NpgsqlCommand($"truncate table {tableName}", conn))
                {
                    truncateCmd.ExecuteNonQuery();
                }

                // Write guids to id
                using (var trans = conn.BeginTransaction())
                {
                    using (var insertCmd = new NpgsqlCommand($"INSERT INTO {tableName} (networkElementId) VALUES (@p)", conn, trans))
                    {
                        var Idparam = insertCmd.Parameters.Add("p", NpgsqlTypes.NpgsqlDbType.Uuid);

                        foreach (var guid in guids)
                        {
                            Idparam.Value = guid;
                            insertCmd.ExecuteNonQuery();
                        }
                    }

                    trans.Commit();
                }
            }
        }

        public void AddGuidsToTable(string tableName, IEnumerable<Guid> guids)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Write guids to table
                using (var trans = conn.BeginTransaction())
                {
                    using (var insertCmd = new NpgsqlCommand($"INSERT INTO {tableName} (networkElementId) VALUES (@p)", conn, trans))
                    {
                        var Idparam = insertCmd.Parameters.Add("p", NpgsqlTypes.NpgsqlDbType.Uuid);

                        foreach (var guid in guids)
                        {
                            Idparam.Value = guid;
                            insertCmd.ExecuteNonQuery();
                        }
                    }

                    trans.Commit();
                }
            }
        }

        public void DeleteGuidsFromTable(string tableName, IEnumerable<Guid> guids)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Delete guids
                using (var trans = conn.BeginTransaction())
                {
                    using (var insertCmd = new NpgsqlCommand($"DELETE FROM {tableName} WHERE networkElementId = @p", conn, trans))
                    {
                        var Idparam = insertCmd.Parameters.Add("p", NpgsqlTypes.NpgsqlDbType.Uuid);

                        foreach (var guid in guids)
                        {
                            Idparam.Value = guid;
                            insertCmd.ExecuteNonQuery();
                        }
                    }

                    trans.Commit();
                }
            }
        }

        private NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_databaseSetting.Value.ConnectionString);
        }
    }
}
