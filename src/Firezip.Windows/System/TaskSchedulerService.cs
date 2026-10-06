namespace Firezip.Windows.System;

[Obsolete("Use Firezip.Infrastructure.Services.TaskSchedulerService instead.")]
public class TaskSchedulerService : Firezip.Infrastructure.Services.TaskSchedulerService
{
    public TaskSchedulerService(Firezip.Core.Interfaces.ILoggingService? loggingService = null) : base(loggingService) { }
}
