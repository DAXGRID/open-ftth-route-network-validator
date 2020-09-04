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
        private readonly DatabaseSetting _databaseSetting;

        public PostgresWriter(ILogger<PostgresWriter> logger, IOptions<DatabaseSetting> databaseSetting)
        {
            _logger = logger;
            _databaseSetting = databaseSetting.Value;
        }

        public IDbConnection GetConnection()
        {
            return new NpgsqlConnection($"Host={_databaseSetting.Host};Port={_databaseSetting.Port};Username={_databaseSetting.Username};Password={_databaseSetting.Password};Database={_databaseSetting.Database}");
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
            string createTableCmdText = $"CREATE UNLOGGED TABLE {schemaName}.{tableName} (networkElementId uuid, PRIMARY KEY(networkElementId));";

            _logger.LogDebug($"Execute SQL: {createTableCmdText}");

            RunDbCommand(transaction, createTableCmdText);
        }

        public void TruncateAndWriteGuidsToTable(string schemaName, string tableName, IEnumerable<Guid> guids, IDbTransaction trans = null)
        {
            if (trans != null)
            {

                // Truncate the table
                using (var truncateCmd = new NpgsqlCommand($"truncate table {schemaName}.{tableName}", (NpgsqlConnection)trans.Connection, (NpgsqlTransaction)trans))
                {
                    truncateCmd.ExecuteNonQuery();
                }

                using (var writer = ((NpgsqlConnection)trans.Connection).BeginBinaryImport($"copy {schemaName}.{tableName} (networkElementId) from STDIN (FORMAT BINARY)"))
                {
                    foreach (var guid in guids)
                    {
                        writer.WriteRow(guid);
                    }

                    writer.Complete();
                }
            }
            else
            {
                using (var conn = GetConnection() as NpgsqlConnection)
                {
                    conn.Open();

                    // Truncate the table
                    using (var truncateCmd = new NpgsqlCommand($"truncate table {schemaName}.{tableName}", conn))
                    {
                        truncateCmd.ExecuteNonQuery();
                    }

                    using (var writer = conn.BeginBinaryImport($"copy {schemaName}.{tableName} (networkElementId) from STDIN (FORMAT BINARY)"))
                    {
                        foreach (var guid in guids)
                        {
                            writer.WriteRow(guid);
                        }

                        writer.Complete();
                    }
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
