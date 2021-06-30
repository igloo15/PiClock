using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
