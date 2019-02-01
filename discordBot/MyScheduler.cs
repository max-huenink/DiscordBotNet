using System;
using discordBot.Services;

namespace discordBot
{
    public static class MyScheduler
    {
        // Most of the source (01/31/2019)
        // https://codinginfinite.com/creating-scheduler-task-seconds-minutes-hours-days/


        /// <summary>
        /// Run once immediately
        /// </summary>
        /// <param name="task">The  task to run</param>
        public static void RunOnceNow(Action task)
        {
            SchedulerService.Instance.ScheduleTask(TimeSpan.FromMilliseconds(-1), task, null);
        }

        /// <summary>
        /// Run once at specified time and don't run again
        /// </summary>
        /// <param name="start">The time to run</param>
        /// <param name="task">The task to run</param>
        public static void RunOnce(DateTime start, Action task)
        {
            SchedulerService.Instance.ScheduleTask(start, TimeSpan.FromMilliseconds(-1), task);
        }

        /// <summary>
        /// Create a new specified task that runs at the first time and runs every interval
        /// </summary>
        /// <param name="start">The time to start</param>
        /// <param name="interval">The interval to run</param>
        /// <param name="task">The task to run</param>
        /// <param name="identifier">The identifier of the task</param>
        public static void NewTask(DateTime start, TimeSpan interval, Action task, string identifier)
        {
            SchedulerService.Instance.ScheduleTask(start, interval, task);
        }

        /*
        public static void IntervalInSeconds(int hour, int sec, double interval, Action task)
        {
            interval /= 3600;
            SchedulerService.Instance.ScheduleTask(hour, sec, interval, task);
        }
        public static void IntervalInMinutes(int hour, int min, double interval, Action task)
        {
            interval /= 60;
            SchedulerService.Instance.ScheduleTask(hour, min, interval, task);
        }
        public static void IntervalInHours(int hour, int min, double interval, Action task)
        {
            SchedulerService.Instance.ScheduleTask(hour, min, interval, task);
        }
        public static void IntervalInDays(int hour, int min, double interval, Action task)
        {
            interval *= 24;
            SchedulerService.Instance.ScheduleTask(hour, min, interval, task);
        }
        */
    }
}
