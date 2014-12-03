using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.PID
{
    class Monitor
    {
        private string name = "";

        // boundaries
        private double lower;
        private double upper;
        private double rate;

        private bool bActive = false;
        internal bool bShow = false;

        internal double current = 0;
        internal double diff = 0;
        internal double previous = 0;

        private double boundKp;
        private double rateKp;

        public Monitor(double Lower, double Upper, double Rate, double BoundKp, double RateKp, string Name)
        {
            lower = Lower;
            upper = Upper;
            rate = Rate;
            boundKp = BoundKp;
            rateKp = RateKp;
            name = Name;
        }

        public float response(double val)
        {
            double res = 0;
            double dt = TimeWarp.fixedDeltaTime;

            previous = current;
            current = val;

            diff = (val - previous) / dt * 0.02 + diff * 0.98;

            if (!bActive) // return 0 if monitor is inactive
                return (float)res;

            // boundary monitor
            if (val > upper)
            {
                res += boundKp * (val - upper); // +ve change
            }
            else if (val < lower)
            {
                res += boundKp * (val - lower); // -ve change
            }

            // rate of change monitor
            if (Math.Abs(diff) > Math.Abs(rate))
            {
                res += rateKp * (diff - rate);
            }

            return (float)res;
        }

        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
            }
        }

        /// <summary>
        /// Lower limit
        /// </summary>
        public double Lower
        {
            get
            {
                return lower;
            }
            set
            {
                lower = value;
            }
        }

        /// <summary>
        /// Upper limit
        /// </summary>
        public double Upper
        {
            get
            {
                return upper;
            }
            set
            {
                upper = value;
            }
        }

        /// <summary>
        /// Rate of change limit
        /// </summary>
        public double Rate
        {
            get
            {
                return rate;
            }
            set
            {
                rate = value;
            }
        }

        /// <summary>
        /// ouput gain on exceeding static limits
        /// </summary>
        public double BoundKp
        {
            get
            {
                return boundKp;
            }
            set
            {
                boundKp = value;
            }
        }

        /// <summary>
        /// output gain on exceeding rate of change limits
        /// </summary>
        public double RateKp
        {
            get
            {
                return rateKp;
            }
            set
            {
                rateKp = value;
            }
        }

        public bool Active
        {
            get
            {
                return bActive;
            }
            set
            {
                bActive = value;
            }
        }
    }
}
