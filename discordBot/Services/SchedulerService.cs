using System;
using System.Collections.Generic;
using System.Threading;

namespace discordBot.Services
{
    public class SchedulerService
    {
        // Most of the source (01/31/2019)
        // https://codinginfinite.com/creating-scheduler-task-seconds-minutes-hours-days/

        private static SchedulerService _instance;
        private List<Timer> timers = new List<Timer>();
        private SchedulerService() { }
        public static SchedulerService Instance => _instance ?? (_instance = new SchedulerService());

        /// <summary>
        /// Schedule a task to run at specified time, every interval
        /// </summary>
        /// <param name="firstRun">The DateTime to run the task at</param>
        /// <param name="interval">How often to run the task, TimeSpan.FromMilliseconds(-1) for never again</param>
        /// <param name="task">The task to run</param>
        /// <param name="identifier">The identifier of the task to be easily deletable</param>
        public void ScheduleTask(DateTime firstRun, TimeSpan interval, Action task)
        {
            DateTime now = DateTime.Now;
            while (now > firstRun)
                firstRun = firstRun.Add(interval);
            
            TimeSpan timeToGo = firstRun - now;
            if (timeToGo <= TimeSpan.Zero)
                timeToGo = TimeSpan.Zero;

            Timer timer = new Timer(x =>
            {
                task.Invoke();
            }, null, timeToGo, interval);

            timers.Add(timer);
        }

        /// <summary>
        /// Schedule a task to run at specified time, every interval
        /// </summary>
        /// <param name="hour">The hour (24) to start the task</param>
        /// <param name="min">The minute to start the task</param>
        /// <param name="interval">How often to run the task, TimeSpan.FromMilliseconds(-1) for never again</param>
        /// <param name="task">The task to run</param>
        /// <param name="identifier">The identifier of the task to be easily deletable</param>
        public void ScheduleTask(int hour, int min, TimeSpan interval, Action task)
        {
            DateTime now = DateTime.Now;
            DateTime firstRun = new DateTime(now.Year, now.Month, now.Day, hour, min, 0, 0);

            ScheduleTask(firstRun, interval, task);
        }

        /// <summary>
        /// Schedule a task to run at a specified time, every interval
        /// </summary>
        /// <param name="hour">The hour (24) to start the task</param>
        /// <param name="min">The minute to start the task</param>
        /// <param name="intervalInHour">The interval in hours to run the task (-1 to never run again)</param>
        /// <param name="task">The task to run</param>
        /// <param name="identifier">The identifier of the task to be easily deletable</param>
        public void ScheduleTask(int hour, int min, double intervalInHour, Action task, string identifier = null)
        {
            DateTime now = DateTime.Now;
            DateTime firstRun = new DateTime(now.Year, now.Month, now.Day, hour, min, 0, 0);

            ScheduleTask(firstRun, TimeSpan.FromHours(intervalInHour), task);
        }

        /// <summary>
        /// Run a task immediately at the specified interval
        /// </summary>
        /// <param name="interval">How often to run the task, TimeSpan.FromMilliseconds(-1) for never again</param>
        /// <param name="task">The task to run</param>
        /// <param name="identifier">The identifier of the task to be easily deletable</param>
        public void ScheduleTask(TimeSpan interval, Action task, string identifier = null)
        {
            Timer timer = new Timer(x =>
            {
                task.Invoke();
            }, null, TimeSpan.Zero, interval);

            timers.Add(timer);
        }
    }
}