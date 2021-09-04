using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PiWebService
{
    public class PiStatus
    {
        public double Humidity { get; set; }

        public double Temp { get; set; }

        public double HeatIndex { get; set; }

        public double DewPoint { get; set; }

        public int ErrorsSinceLastUpdate { get; set; }

        public int TimeSinceLastUpdate { get; set; }

        public long Time { get; set; }

        public PiClockStatus[] Clocks { get; set; }
    }

    public class PiClockStatus
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Status { get; set; }

        public long TimeInSeconds { get; set; }

        internal void Update(PiClockStatus newClock) {
            Status = newClock.Status;
            TimeInSeconds = newClock.TimeInSeconds;
        }
    }

    public class PiCommand
    {
        public string Name { get; set; }

        public JsonDocument Data { get; set; }
    }
}
