using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.Net;
using System.IO;

namespace EnphaseParser
{
	public class EnphaseDataClient
	{

		/// <summary>
		/// Gets the URL for the server this client is "connected to"
		/// </summary>
		public string ServerUrl
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the list of events
		/// </summary>
		public List<EnphaseEvent> Events
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the production status of the enphase array
		/// </summary>
		public EnphaseProduction Production
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets a list of statuses for inverters in teh array
		/// </summary>
		public List<EnphaseInverterStatus> InverterStatus
		{
			get;
			private set;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="url">URL of the enphase server</param>
		public EnphaseDataClient(string url)
		{
			this.ServerUrl = url;
			this.Events = new List<EnphaseEvent>();
			this.InverterStatus = new List<EnphaseInverterStatus>();
		}

		/// <summary>
		/// Updates system data from the server
		/// </summary>
		public void Update(bool updateEventList, bool updateProductionStatus, bool updateInverterStatus)
		{
			if (updateEventList)
			{
				this.UpdateEventData();
			}
			if (updateProductionStatus)
			{
				this.UpdateProductionData();
			}
			if (updateInverterStatus)
			{
				this.UpdateStatusData();
			}
		}

		/// <summary>
		/// Updates list of events from the EnPhase system
		/// </summary>
		private void UpdateEventData()
		{
			string contents = this.GetPageContents(this.ServerUrl + "/home");
			
			// Find the "Events" header
			int startPos = contents.IndexOf("<h2>Events</h2>");
			if (startPos == -1)
			{
				// Bad Bad Bad
				throw new Exception("Could not find start of events section");
			}

			// From here, find the next table's start
			int startTablePos = contents.IndexOf("<table", startPos);
			if (startTablePos == -1)
			{
				// Bad Bad Bad
				throw new Exception("Could not find start of events table");
			}

			// From here, find the end of the table
			int endTablePos = contents.IndexOf("</table>", startPos);
			if (endTablePos == -1)
			{
				// Bad Bad Bad
				throw new Exception("Could not find end of events section");
			}

			// Get table contents
			string tableContents = contents.Substring(startTablePos, endTablePos - startTablePos);

			// Split by "<tr>". This will get us all the rows
			string[] rows = tableContents.Split(new string[] { "<tr>", "</tr>\n" }, StringSplitOptions.RemoveEmptyEntries);
			
			// Process each row to get an event
			this.Events.Clear();
			foreach (string row in rows)
			{
				if (row.StartsWith("<td"))
				{
					var e = EnphaseEvent.Parse(row);
					if (e != null)
					{
						this.Events.Add(e);
					}
				}
			}
		}

		/// <summary>
		/// Updates production/generation information
		/// </summary>
		private void UpdateProductionData()
		{
			string contents = this.GetPageContents(this.ServerUrl + "/production");

			// Find the "Events" header
			int startPos = contents.IndexOf("<h1>System Energy Production</h1>");
			if (startPos == -1)
			{
				// Bad Bad Bad
				throw new Exception("Could not find start of production section");
			}

			// From here, find the next table's start
			int startTablePos = contents.IndexOf("<table", startPos);
			if (startTablePos == -1)
			{
				// Bad Bad Bad
				throw new Exception("Could not find start of production table");
			}

			// From here, find the end of the table
			int endTablePos = contents.IndexOf("</table>", startPos);
			if (endTablePos == -1)
			{
				// Bad Bad Bad
				throw new Exception("Could not find end of production section");
			}

			// Get table contents
			string tableContents = contents.Substring(startTablePos, endTablePos - startTablePos);

			// Split by "<tr>". This will get us all the rows
			string[] rows = tableContents.Replace("\t", "").Replace("\n", "").Split(new string[] { "<tr>", "</tr>\n" }, StringSplitOptions.RemoveEmptyEntries);

			// Process rows to get production data

			// Row 1 is uptime
			DateTime uptime = DateTime.MinValue;
			
			string uptimeSource = rows[1];
			int startIndex = uptimeSource.IndexOf("<div");
			if (startIndex == -1)
			{
				throw new Exception("Parse tokens not found");
			}
			startIndex = uptimeSource.IndexOf(">", startIndex + 1) + 1;
			int endIndex = uptimeSource.IndexOf("</div>", startIndex + 1);
			string uptimeString = uptimeSource.Substring(startIndex, endIndex - startIndex);
			uptime = DateTime.Parse(uptimeString);

			// Row 3 is current
			double current = -1;

			string[] currentParts = (rows[3].Split(new string[] { "<td>", "</td><td>", "</td></tr>" }, StringSplitOptions.RemoveEmptyEntries))[1].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
			current = double.Parse(currentParts[0]);
			if (currentParts[1].ToLowerInvariant() == "kw")
			{
				current *= 1000;
			}
	
			// Row 4 is today
			double today = -1;
	
			string[] todayParts = (rows[4].Split(new string[] { "<td>", "</td><td>", "</td></tr>" }, StringSplitOptions.RemoveEmptyEntries))[1].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
			today = double.Parse(todayParts[0]);
			if (todayParts[1].ToLowerInvariant() == "kw")
			{
				today *= 1000;
			}
			
			// Row 4 is this past week
			double week = -1;
			
			string[] weekParts = (rows[5].Split(new string[] { "<td>", "</td><td>", "</td></tr>" }, StringSplitOptions.RemoveEmptyEntries))[1].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
			week = double.Parse(weekParts[0]);
			if (weekParts[1].ToLowerInvariant() == "kw")
			{
				week *= 1000;
			}
			
			// Row 5 is since installation
			double sinceInstall = -1;

			string[] sinceInstallParts = (rows[6].Split(new string[] { "<td>", "</td><td>", "</td></tr>" }, StringSplitOptions.RemoveEmptyEntries))[1].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
			sinceInstall = double.Parse(sinceInstallParts[0]);
			if (sinceInstallParts[1].ToLowerInvariant() == "kw")
			{
				sinceInstall *= 1000;
			}

			// Update production data
			this.Production = new EnphaseProduction(uptime, current, today, week, sinceInstall);

		}

		/// <summary>
		/// Updates microinverter status information
		/// </summary>
		private void UpdateStatusData()
		{
			string contents = this.GetPageContents(this.ServerUrl + "/inventory");

			// Find the "Events" header
			int startPos = contents.IndexOf("<h3>Microinverter</h3>");
			if (startPos == -1)
			{
				// Bad Bad Bad
				throw new Exception("Could not find start of inventory section");
			}

			// From here, find the next table's start
			int startTablePos = contents.IndexOf("<table", startPos);
			if (startTablePos == -1)
			{
				// Bad Bad Bad
				throw new Exception("Could not find start of inventory table");
			}

			// From here, find the end of the table
			int endTablePos = contents.IndexOf("</table>", startPos);
			if (endTablePos == -1)
			{
				// Bad Bad Bad
				throw new Exception("Could not find end of inventory section");
			}

			// Get table contents
			string tableContents = contents.Substring(startTablePos, endTablePos - startTablePos);

			// Split me some rows and parse
			this.InverterStatus.Clear();
			string[] rows = tableContents.Replace("\n", "").Replace("\t", "").Split(new string[] { "<tr>", "</tr>" }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string row in rows)
			{
				if (row.StartsWith("<td"))
				{
					var status = EnphaseInverterStatus.Parse(row);
					if (status != null)
					{
						this.InverterStatus.Add(status);
					}
				}
			}
		}

		/// <summary>
		/// Gets contents of the page specified
		/// </summary>
		/// <param name="url">URL to the page to get contents of</param>
		/// <returns>Contents of page specified in the url</returns>
		private string GetPageContents(string url)
		{
			// This way doesn't support timeouts.
			//var client = new System.Net.WebClient();
			//client.Proxy = null;
			//return client.DownloadString(url);

			WebRequest client = WebRequest.Create(url);
			client.Timeout = 1000 * 300; // five minutes
			WebResponse resp = client.GetResponse();
			StreamReader rdr = new StreamReader(resp.GetResponseStream());
			return rdr.ReadToEnd();
		}

	}
}
