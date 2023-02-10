using System;

namespace OpenFTTH.RouteNetwork.Validator.Notification;

internal interface INotificationClient : IDisposable
{
    void Notify(string notificationHeader, string notificationBody);
}
