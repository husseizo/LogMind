using LogMind.Core.Models;

namespace LogMind.Core.Interfaces;

public interface INotificationService
{
    Task NotifyAlertAsync(Alert alert);
}
