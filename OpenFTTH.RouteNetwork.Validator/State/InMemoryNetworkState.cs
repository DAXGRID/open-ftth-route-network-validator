using DAX.ObjectVersioning.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.RouteNetwork.Validator.Config;
using OpenFTTH.RouteNetwork.Validator.Database.Impl;
using OpenFTTH.RouteNetwork.Validator.Model;
using OpenFTTH.RouteNetwork.Validator.Validators;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator.State
{
    public class InMemoryNetworkState
    {
        private readonly ILogger<InMemoryNetworkState> _logger;

        private readonly PostgresWriter _postgresWriter;

        private readonly IOptions<DatabaseSetting> _databaseSetting;

        private readonly IServiceProvider _serviceProvider;

        private InMemoryObjectManager _objectManager = new InMemoryObjectManager();
        public InMemoryObjectManager ObjectManager => _objectManager;

        private bool _loadMode = true;

        private ITransaction _loadModeTransaction;

        private ITransaction _cmdTransaction;

        private DateTime __lastEventRecievedTimestamp = DateTime.UtcNow;
        public DateTime LastEventRecievedTimestamp => __lastEventRecievedTimestamp;
      
        private long _numberOfObjectsLoaded = 0;
        public long NumberOfObjectsLoaded => _numberOfObjectsLoaded;

        public InMemoryNetworkState(ILogger<InMemoryNetworkState> logger, PostgresWriter postgresWriter, IOptions<DatabaseSetting> databaseSetting, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _postgresWriter = postgresWriter;
            _databaseSetting = databaseSetting;
            _serviceProvider = serviceProvider;
        }

        public ITransaction GetTransaction()
        {
            if (_loadMode)
                return GetLoadModeTransaction();
            else
                return GetCommandTransaction();
        }

        public void FinishWithTransaction()
        {
            __lastEventRecievedTimestamp = DateTime.UtcNow;
            _numberOfObjectsLoaded++;

            // We're our of load mode, and dealing with last event
            if (!_loadMode && _loadModeTransaction == null)
            {
                // Commit the command transaction
                _cmdTransaction.Commit();
                _cmdTransaction = null;

                CallValidators(false);
            }
        }

        private void CallValidators(bool initial)
        {
            var validators = _serviceProvider.GetServices<IValidator>().ToList();

            if (initial)
            {
                using (var conn = _postgresWriter.GetConnection())
                {
                    conn.Open();

                    using (var trans = conn.BeginTransaction())
                    {
                        // Drop and create schema
                        _postgresWriter.DropSchema(_databaseSetting.Value.Schema, trans);
                        _postgresWriter.CreateSchema(_databaseSetting.Value.Schema, trans);

                        // Create validator tables
                        foreach (var validator in validators)
                        {
                            validator.CreateTable(trans);
                        }

                        // Do initial validation
                        foreach (var validator in validators)
                        {
                            validator.Validate(true, trans);
                        }

                        trans.Commit();
                    }
                }
            }
            else
            {
                // Do validation
                foreach (var validator in validators)
                {
                    validator.Validate(false);
                }
            }
        }

        public IVersionedObject GetObject(Guid id)
        {
            if (_loadMode && _loadModeTransaction != null)
                return _loadModeTransaction.GetObject(id);
            else if (_cmdTransaction != null)
            {
                var transObj = _cmdTransaction.GetObject(id);

                if (transObj != null)
                    return transObj;
                else
                    return _objectManager.GetObject(id);
            }
            else
                return null;
        }


        public void FinishLoadMode()
        {
            _loadMode = false;

            if (_loadModeTransaction != null)
                _loadModeTransaction.Commit();

            _loadModeTransaction = null;

            CallValidators(true);
        }


        private ITransaction GetLoadModeTransaction()
        {
            if (_loadModeTransaction == null)
                _loadModeTransaction = _objectManager.CreateTransaction();

            return _loadModeTransaction;
        }

        private ITransaction GetCommandTransaction()
        {
            if (_cmdTransaction == null)
                _cmdTransaction = _objectManager.CreateTransaction();

            return _cmdTransaction;
        }
    }
}
