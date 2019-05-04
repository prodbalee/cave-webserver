using System;
using System.Net;
using System.Threading;
using Cave.Collections.Generic;

namespace Cave.Web
{
    /// <summary>
    /// Provides a firewall entry.
    /// </summary>
    /// <seealso cref="IExpiring" />
    class WebFirewallEntry : IExpiring
    {
        /// <summary>The source address.</summary>
        public readonly string Address;

        /// <summary>Gets the error counter.</summary>
        /// <value>The error counter.</value>
        public int ErrorCounter { get; private set; }

        long lastAccessTicks;

        /// <summary>Gets a value indicating whether this instance is expired.</summary>
        /// <value>
        /// <c>true</c> if this instance is expired; otherwise, <c>false</c>.
        /// </value>
        public bool IsExpired()
        {
            return DateTime.UtcNow.Ticks > Interlocked.Read(ref lastAccessTicks) + TimeSpan.TicksPerMinute;
        }

        /// <summary>Initializes a new instance of the <see cref="WebFirewallEntry"/> class.</summary>
        /// <param name="source">The source endPoint.</param>
        public WebFirewallEntry(IPEndPoint source)
        {
            Address = source.Address.ToString();
        }

        /// <summary>Updates this instance with a successful request resetting all errors.</summary>
        public void Success()
        {
            Interlocked.Exchange(ref lastAccessTicks, DateTime.UtcNow.Ticks);
            ErrorCounter = 0;
        }

        /// <summary>Adds an error to this instance.</summary>
        public void Error()
        {
            Interlocked.Exchange(ref lastAccessTicks, DateTime.UtcNow.Ticks);
            ErrorCounter++;
        }

        public TimeSpan GetWaitTime(int errorCount = -1)
        {
            if (errorCount == -1)
            {
                errorCount = ErrorCounter;
            }

            if (errorCount <= 0)
            {
                return TimeSpan.Zero;
            }

            // calculate earliest start ticks
            long pow = errorCount * errorCount;
            long ticks = Interlocked.Read(ref lastAccessTicks) + (100 * pow * TimeSpan.TicksPerMillisecond);

            // at least one second
            ticks += TimeSpan.TicksPerSecond;
            ticks -= DateTime.UtcNow.Ticks;
            return new TimeSpan(ticks);
        }

        int waitingCount;

        public int Enter()
        {
            return Interlocked.Increment(ref waitingCount);
        }

        internal int Exit()
        {
            return Interlocked.Decrement(ref waitingCount);
        }
    }
}
