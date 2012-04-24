using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnphaseParser
{
	/// <summary>
	/// Enphase server event
	/// </summary>
	public class EnphaseEvent
	{
		/// <summary>
		/// Gets the ID for the event
		/// </summary>
		public int EventID
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the message for the event
		/// </summary>
		public string Message
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the device ID that this event is from
		/// </summary>
		public string Device
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the timestamp for the event
		/// </summary>
		public DateTime Timestamp
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the details for this event
		/// </summary>
		public string Details
		{
			get;
			private set;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="eventID"></param>
		/// <param name="message"></param>
		/// <param name="device"></param>
		/// <param name="timestamp"></param>
		/// <param name="details"></param>
		public EnphaseEvent(int eventID, string message, string device, DateTime timestamp, string details)
		{
			this.EventID = eventID;
			this.Message = message;
			this.Device = device;
			this.Timestamp = timestamp;
			this.Details = details;
		}

		/// <summary>
		/// Parse an HTML row to make an event
		/// </summary>
		/// <param name="rowToParse"></param>
		/// <returns></returns>
		public static EnphaseEvent Parse(string rowToParse)
		{
			string[] parts = rowToParse.Replace("\n", "").Split(new string[] { "<td>", "</td>" }, StringSplitOptions.RemoveEmptyEntries);
			int eventID = int.Parse(parts[0]);
			string message = parts[1];
			string device = parts[2];
			DateTime timestamp = DateTime.Parse(parts[3]);
			string details = "";
			if (parts.Length > 4)
			{
				details = parts[4];
			}
			return new EnphaseEvent(eventID, message, device, timestamp, details);
		}
	}
}
