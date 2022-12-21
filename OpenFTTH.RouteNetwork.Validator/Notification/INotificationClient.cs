using System;

namespace OpenFTTH.RouteNetwork.Validator.Notification;

public interface INotificationClient : IDisposable
{
    void Notify(string notificationHeader, string notificationBody);
}
