
namespace PilotAssistant.PID
{
    /// <summary>
    /// Holds the values for a single controller
    /// </summary>
    public class PIDConstants
    {
        /// <summary>
        /// private array holds values, access through properties
        /// </summary>
        double[] values;

        public PIDConstants()
        {
            values = new double[9] { 0, 0, 0, -1, 1, -1, 1, 1, 1 };
        }

        public PIDConstants(double[] gains)
        {
            values = gains;
        }

        public double KP
        {
            get
            {
                return values[0];
            }
            set
            {
                values[0] = value;
            }
        }

        public double KI
        {
            get
            {
                return values[1];
            }
            set
            {
                values[1] = value;
            }
        }

        public double KD
        {
            get
            {
                return values[2];
            }
            set
            {
                values[2] = value;
            }
        }

        public double OutMin
        {
            get
            {
                return values[3];
            }
            set
            {
                values[3] = value;
            }
        }

        public double OutMax
        {
            get
            {
                return values[4];
            }
            set
            {
                values[4] = value;
            }
        }

        public double IMin
        {
            get
            {
                return values[5];
            }
            set
            {
                values[5] = value;
            }
        }

        public double IMax
        {
            get
            {
                return values[6];
            }
            set
            {
                values[6] = value;
            }
        }

        public double Scalar
        {
            get
            {
                return values[7];
            }
            set
            {
                values[7] = value;
            }
        }

        public double Easing
        {
            get
            {
                return values[8];
            }
            set
            {
                values[8] = value;
            }
        }
    }
}
