using DAX.ObjectVersioning.Graph;
using OpenFTTH.Events.RouteNetwork.Infos;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator.Model
{
    public class RouteNode : GraphNode
    {
        private readonly RouteNodeFunctionEnum? _function;
        public RouteNodeFunctionEnum? Function => _function;

        private readonly string _name;
        public string Name => _name;

        public RouteNode(Guid id, RouteNodeFunctionEnum? function, string name = null) : base(id)
        {
            _function = function;
            _name = name;
        }
    }
}
