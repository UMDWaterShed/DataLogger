using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;
using NLog;

namespace WaterShed.DataLogger
{
	class DataPoint
	{
		public DateTime Timestamp { get; set; }
		public string System { get; set; }
		public string Sensor { get; set; }
		public string Value { get; set; }

		private static Logger logger = LogManager.GetCurrentClassLogger();

		public DataPoint(string system, string sensor, string value)
		{
			Timestamp = DateTime.Now;
			System = system;
			Sensor = sensor;
			Value = value;
		}

		public void Save(MySqlConnection dbconn)
		{
			string commandText = 
@"INSERT INTO datapoints (`timestamp`, `system`, `sensor`, `value`)
VALUES (?Time, ?Sys, ?Sensor, ?Val)";

			MySqlCommand cmd = new MySqlCommand(commandText, dbconn);

			cmd.Parameters.AddWithValue("?Time", Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
			cmd.Parameters.AddWithValue("?Sys", System);
			cmd.Parameters.AddWithValue("?Sensor", Sensor);
			cmd.Parameters.AddWithValue("?Val", Value);

			cmd.ExecuteNonQuery();

			logger.Trace("DataPoint Saved!");
		}
	}
}
