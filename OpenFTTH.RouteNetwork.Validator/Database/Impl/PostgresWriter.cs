using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenFTTH.RouteNetwork.Validator.Config;
using System;
using System.Collections.Generic;
using System.Data;

namespace OpenFTTH.RouteNetwork.Validator.Database.Impl
{
    public class PostgresWriter
    {
        private readonly ILogger<PostgresWriter> _logger;
        private readonly IOptions<DatabaseSetting> _databaseSetting;

        public PostgresWriter(ILogger<PostgresWriter> logger, IOptions<DatabaseSetting> databaseSetting)
        {
            _logger = logger;
            _databaseSetting = databaseSetting;
        }

        public IDbConnection GetConnection()
        {
            return new NpgsqlConnection(_databaseSetting.Value.ConnectionString);
        }


        public void CreateSchema(string schemaName, IDbTransaction transaction = null)
        {
            string createSchemaCmdText = $"CREATE SCHEMA IF NOT EXISTS {schemaName};";

            _logger.LogDebug($"Execute SQL: {createSchemaCmdText}");

            RunDbCommand(transaction, createSchemaCmdText);
        }

        public void DropSchema(string schemaName, IDbTransaction transaction = null)
        {
            string deleteSchemaCmdText = $"DROP SCHEMA IF EXISTS {schemaName} CASCADE;";

            _logger.LogDebug($"Execute SQL: {deleteSchemaCmdText}");

            RunDbCommand(transaction, deleteSchemaCmdText);
        }

        /// <summary>
        /// Create simple table used to store ids representing a result of some validation
        /// </summary>
        /// <param name="schemaName"></param>
        /// <param name="tableName"></param>
        /// <param name="transaction"></param>
        public void CreateIdTable(string schemaName, string tableName, IDbTransaction transaction = null)
        {
            string createTableCmdText = $"CREATE TABLE {schemaName}.{tableName} (networkElementId uuid, PRIMARY KEY(networkElementId));";

            _logger.LogDebug($"Execute SQL: {createTableCmdText}");

            RunDbCommand(transaction, createTableCmdText);
        }

        public void TruncateAndWriteGuidsToTable(string schemaName, string tableName, IEnumerable<Guid> guids)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Truncate the table
                using (var truncateCmd = new NpgsqlCommand($"truncate table {schemaName}.{tableName}", (NpgsqlConnection)conn))
                {
                    truncateCmd.ExecuteNonQuery();
                }

                // Write guids to id
                using (var trans = conn.BeginTransaction())
                {
                    using (var insertCmd = new NpgsqlCommand($"INSERT INTO {schemaName}.{tableName} (networkElementId) VALUES (@p)", (NpgsqlConnection)conn, (NpgsqlTransaction)trans))
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

        public void AddGuidsToTable(string schemaName, string tableName, IEnumerable<Guid> guids)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Write guids to table
                using (var trans = conn.BeginTransaction())
                {
                    using (var insertCmd = new NpgsqlCommand($"INSERT INTO {schemaName}.{tableName} (networkElementId) VALUES (@p)", (NpgsqlConnection)conn, (NpgsqlTransaction)trans))
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

        public void DeleteGuidsFromTable(string schemaName, string tableName, IEnumerable<Guid> guids)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Delete guids
                using (var trans = conn.BeginTransaction())
                {
                    using (var insertCmd = new NpgsqlCommand($"DELETE FROM {schemaName}.{tableName} WHERE networkElementId = @p", (NpgsqlConnection)conn, (NpgsqlTransaction)trans))
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

        private void RunDbCommand(IDbTransaction transaction, string createTableCmdText)
        {
            if (transaction == null)
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var createSchemaCmd = new NpgsqlCommand(createTableCmdText, (NpgsqlConnection)conn))
                    {
                        createSchemaCmd.ExecuteNonQuery();
                    }
                }
            }
            else
            {
                using (var createSchemaCmd = new NpgsqlCommand(createTableCmdText, (NpgsqlConnection)transaction.Connection, (NpgsqlTransaction)transaction))
                {
                    createSchemaCmd.ExecuteNonQuery();
                }
            }
        }

    }
}
