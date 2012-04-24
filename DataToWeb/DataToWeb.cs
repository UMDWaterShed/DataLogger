using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using MySql.Data.MySqlClient;
using NLog;
using System.Timers;
using WaterShed.DataToWeb.Properties;
using System.Net;
using System.IO;
using System.Security.Cryptography;

namespace WaterShed.DataToWeb
{
	public partial class DataToWeb : ServiceBase
	{
		private MySqlConnection dbconn;

		private static Logger logger = LogManager.GetCurrentClassLogger();

		private Timer main_timer;

		private void AppStart(String[] args)
		{
			main_timer = new Timer();

			logger.Info("DataToWeb Starting up.");

			dbconn = new MySqlConnection(DB.GetDBConn());
			try
			{
				dbconn.Open();
			}
			catch (Exception ex)
			{
				logger.ErrorException("Failed to Open Database Connection.", ex);
				//eventLog.WriteEntry("Failed to open DataBase Connection.  Exception: " + ex.Message, EventLogEntryType.Error);
				throw ex; //need to put in more error handling
			}

			// Allow bad cert. - we don't really care
			ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

			main_timer.Elapsed += new ElapsedEventHandler(main_timer_Tick);
			main_timer.Interval = Settings.Default.mainTimer_Interval;
			main_timer.Start();
		}

		bool inTick = false;

		private void main_timer_Tick(object sender, EventArgs e)
		{
			if (!inTick)
			{
				logger.Trace("Tick");
				inTick = true;
			}
			else
			{
				logger.Trace("Tick - Ignored: Already in Tick");
				return;
			}

			uint lastPointRemote = GetLastIDFromServer();
			
			PushData(lastPointRemote);
			
			inTick = false;
		}

		private uint GetLastIDFromServer()
		{
			uint id = 0;
			HttpWebRequest req = null;
			HttpWebResponse res = null;

			try
			{
				string result = "";
				req = (HttpWebRequest)WebRequest.Create(Settings.Default.CheckURL);
				req.Timeout = Settings.Default.Timeout * 1000;
				req.Method = "GET";
				req.ProtocolVersion = HttpVersion.Version10;
				req.KeepAlive = false;

				res = (HttpWebResponse)req.GetResponse();
				
				//if (res.StatusCode == HttpStatusCode.NoContent)
				if (res.StatusCode == HttpStatusCode.OK)
				{
					result = res.GetResponseHeader("X-LastPoint");
				}

				if (result.Length > 0)
				{
					logger.Debug("Last Point: {0}", result);
					id =  uint.Parse(result);
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Could not get last ID from server", ex);
			}

			req = null;
			if (!(res == null))
			{
				try
				{
					res.Close();
				}
				finally
				{
					res = null;
				}
			}

			return id;
		}

		//private void PushData(DateTime fromTime)
		private void PushData(uint fromID)
		{
			List<Dictionary<String, String>> p = new List<Dictionary<String, String>>();

			try
			{
				string commandText = "SELECT `id`, `timestamp`, `system`, `sensor`, `value` FROM datapoints WHERE `id` > ?id " +
					" ORDER BY `id` ASC LIMIT 0, ?numRows";
				MySqlCommand cmd = new MySqlCommand(commandText, dbconn);

				cmd.Parameters.AddWithValue("?id", fromID);
				cmd.Parameters.AddWithValue("?numRows", Settings.Default.NumRowsAtATime);

				using (MySqlDataReader points = cmd.ExecuteReader())
				{
					DataTable schemaTable = points.GetSchemaTable();

					while (points.Read())
					{
						Dictionary<String, String> point = new Dictionary<string, string>();

						foreach (DataRow colname in schemaTable.Rows)
						{
							point.Add(colname[0].ToString(), points.GetString(colname[0].ToString()));
						}

						p.Add(point);
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error retrieving data from the database", ex);
			}

			string data = fastJSON.JSON.Instance.ToJSON(p);
			byte[] dataArray = Encoding.ASCII.GetBytes(data);

            logger.Trace("Data: ({0}) {1}", dataArray.Length, (data.Length < 500 ? data : data.Substring(0, 500) + "..."));

			HttpWebRequest req = null;
			HttpWebResponse res = null;

			try
			{
				req = (HttpWebRequest)WebRequest.Create(Settings.Default.PostURL);
				req.Credentials = new NetworkCredential(Settings.Default.PostUser, Settings.Default.PostPass);
				req.PreAuthenticate = true;
				req.Method = "POST";
				req.UserAgent = "WaterShed DataToWeb/0.0.0.0";
				req.ContentLength = dataArray.Length;
				//req.ContentType = "application/json";
				req.ContentType = "application/x-www-form-urlencoded";
				req.Headers.Add("X-Hash", SHA1(dataArray));
				req.Timeout = Settings.Default.Timeout * 1000;
				req.KeepAlive = false;

				Stream dataStream = req.GetRequestStream();
				dataStream.Write(dataArray, 0, dataArray.Length);
				dataStream.Close();

				res = (HttpWebResponse)req.GetResponse();

				StreamReader sr = new StreamReader(res.GetResponseStream());
				string response = sr.ReadToEnd();

				switch (res.StatusCode)
				{
					case HttpStatusCode.NotModified:
						logger.Info("PushData successful but no data uploaded.");
						break;
					case HttpStatusCode.OK:
						logger.Debug("Data Accepted by server: {0}", response);
						break;
					case HttpStatusCode.Unauthorized:
						logger.Error("No Credentials Provided.");
						break;
					case HttpStatusCode.Forbidden:
						logger.Error("Incorrect Credentials: {0}/{1}", Settings.Default.PostUser, Settings.Default.PostPass);
						break;
					case HttpStatusCode.BadRequest:
						logger.Error("Invalid Hash on uploaded data: {0}", (data.Length < 500 ? data : data.Substring(0,500) + "..."));
						break;
					default:
						logger.Error("Unsuccessful upload: {0}", response);
						break;
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error Uploading data to server", ex);
			}

			req = null;
			if (!(res == null))
			{
				try
				{
					res.Close();
				}
				finally
				{
					res = null;
				}
			}
		}

		[Obsolete("Use ID instead of time.")]
		private DateTime GetLastTimeFromServer()
		{
			string result = "";
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Settings.Default.CheckURL);
			HttpWebResponse res = (HttpWebResponse)req.GetResponse();

			if (res.StatusCode == HttpStatusCode.NoContent)
			{
				result = res.GetResponseHeader("Last-Modified");
			}

			if (result.Length > 0)
			{
				return DateTime.Parse(result);
			}

			return DateTime.MaxValue;
		}


		#region Overrides and Constructors
		protected override void OnStart(string[] args)
		{
			logger.Info("DataToWeb was sent START signal.");
			AppStart(args);
		}
		protected override void OnStop()
		{
			logger.Info("DataToWeb was sent STOP signal.");
			try
			{
				dbconn.Close();
			}
			finally { }
		}

		protected override void OnPause()
		{
			logger.Info("DataToWeb was sent PAUSE signal.");
			main_timer.Stop();
			base.OnPause();
		}

		protected override void OnContinue()
		{
			logger.Info("DataToWeb was sent CONTINUE signal.");
			main_timer.Start();
			base.OnContinue();
		}

		protected override void OnCustomCommand(int command)
		{
			logger.Info("DataToWeb was sent custom signal:" + command.ToString());
			switch (command)
			{
				default:
					base.OnCustomCommand(command);
					break;
			}
		}

		public DataToWeb()
		{
			InitializeComponent();
		}
		#endregion

		/// <summary>
		/// Calculates SHA1 hash
		/// </summary>
		/// <param name="text">input string</param>
		/// <param name="enc">Character encoding</param>
		/// <returns>SHA1 hash</returns>
		public static string SHA1(byte[] text)
		{
			SHA1CryptoServiceProvider cryptoTransformSHA1 =
			new SHA1CryptoServiceProvider();
			string hash = BitConverter.ToString(
				cryptoTransformSHA1.ComputeHash(text)).Replace("-", "");

			return hash;
		}
	}
}
