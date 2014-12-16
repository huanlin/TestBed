using System;

namespace DependencyInjectionDemo
{
    public interface ITimeService : IDisposable
    {
        string Now { get; }
    }

    public class TimeService : ITimeService
    {
        public static int Count;

        public TimeService()
        {
            Count++;
        }

        public string Now
        {
            get
            {
                //return DateTime.Now.ToString();
                return Count.ToString();
            }
        }

        public void Dispose()
        {
            Count--;
        }
    }
}