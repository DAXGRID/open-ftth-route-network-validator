using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator.Validators
{
    public interface IValidator
    {
        public void CreateTable(IDbTransaction transaction);
        public void Validate(bool initial, IDbTransaction trans = null);
    }
}
