using System;

namespace OpenFTTH.Events
{
    /// <summary>
    /// General event attibutes used in most Open FTTH topics
    /// </summary>
    public class DomainEvent : IDomainEvent
    {
        public string EventType { get; set; }
        public Guid EventId { get; set; }
        public string CmdType { get; set; }
        public Guid CmdId { get; set; }
    }
}
