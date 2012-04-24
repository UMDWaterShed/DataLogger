using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;
using WaterShed.DataLogger.Properties;

namespace WaterShed.DataLogger
{
    struct Sensor
    {
        /// <summary>
        /// We can use this so only one thread can query this sensor at a time.
        /// </summary>
        //public object threadsafe;

        public int SensorID;
        public int Type;
        public String Location;
        public int COMPort;
        public int SensorNumber;
        public String ProcessMethod;
        public TimeSpan UpdateFrequency;
        public DateTime LastUpdate;
    }
}
