using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnphaseParser
{
	public class EnphaseInverterStatus
	{
		/// <summary>
		/// Gets the part "number" for the micro inverter
		/// </summary>
		public string PartNumber
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the timestamp for when this was installed
		/// </summary>
		public DateTime Installed
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the serial number for the micro inverter
		/// </summary>
		public string SerialNumber
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the status of the micro inverter
		/// </summary>
		public string Status
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the timestamp when the micro inverter last reported in
		/// </summary>
		public DateTime LastReportTimestamp
		{
			get;
			private set;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="_partNumber"></param>
		/// <param name="_installed"></param>
		/// <param name="_serialNumber"></param>
		/// <param name="_status"></param>
		/// <param name="_lastReport"></param>
		public EnphaseInverterStatus(string _partNumber, DateTime _installed, string _serialNumber, string _status, DateTime _lastReport)
		{
			this.PartNumber = _partNumber;
			this.Installed = _installed;
			this.SerialNumber = _serialNumber;
			this.Status = _status;
			this.LastReportTimestamp = _lastReport;
		}

		/// <summary>
		/// Parses a row of HTML to return status for a micro inverter
		/// </summary>
		/// <param name="rowToParse"></param>
		/// <returns></returns>
		public static EnphaseInverterStatus Parse(string rowToParse)
		{
			string[] parts = rowToParse.Split(new string[] { "<td class=\"tbl_bod\">", "</td>" }, StringSplitOptions.RemoveEmptyEntries);
			string partNumber = parts[0];
			DateTime installed = DateTime.Parse(parts[1]);
			string serial = parts[2];
			string status = parts[3];
			DateTime lastReported = DateTime.Parse(parts[7]);
			return new EnphaseInverterStatus(partNumber, installed, serial, status, lastReported);
		}
	}
}
