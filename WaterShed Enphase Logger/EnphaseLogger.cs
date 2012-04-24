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
using WaterShed.Enphase_Logger.Properties;
using EnphaseParser;

namespace WaterShed.Enphase_Logger
{
	public partial class EnphaseLogger : ServiceBase
	{
		private MySqlConnection dbconn;
		private EnphaseDataClient enphase;
		private static Logger logger = LogManager.GetCurrentClassLogger();
		private Timer main_timer;
		private DateTime lastUpdate = DateTime.Now;
		
		private void AppStart(string[] args)
		{
			main_timer = new Timer();

			logger.Info("DataLogger Starting up.");

			enphase = new EnphaseDataClient(Settings.Default.ServerURL);

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

			main_timer.Elapsed += new ElapsedEventHandler(main_timer_Tick);
			main_timer.Interval = Settings.Default.mainTimer_Interval;
			main_timer.Start();
		}

		private bool isInTick = false;

		private void main_timer_Tick(object sender, EventArgs e)
		{
			if (!isInTick)
			{
				logger.Trace("Tick");
				isInTick = true;
			}
			else
			{
				logger.Trace("Tick - Ignored: Already in Tick");
				return;
			}
			
			try
			{
				enphase.Update(false, true, false);

				(new DataPoint("Enphase", "Now", enphase.Production.Current.ToString())).Save(dbconn);
				(new DataPoint("Enphase", "Today", enphase.Production.Today.ToString())).Save(dbconn);
				(new DataPoint("Enphase", "Week", enphase.Production.PastWeek.ToString())).Save(dbconn);
				(new DataPoint("Enphase", "AllTime", enphase.Production.SinceInstallation.ToString())).Save(dbconn);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Some Kind of Error Occured.", ex);

				//throw ex;
			}
			finally
			{
				// allow the next tick to start
				isInTick = false;
			}
		}

		#region Overrides and Constructors
		protected override void OnStart(string[] args)
		{
			logger.Info("DataLogger was sent START signal.");
			AppStart(args);
		}
		protected override void OnStop()
		{
			logger.Info("DataLogger was sent STOP signal.");
			try
			{
				dbconn.Close();
			}
			finally { }
		}

		protected override void OnPause()
		{
			logger.Info("DataLogger was sent PAUSE signal.");
			main_timer.Stop();
			base.OnPause();
		}

		protected override void OnContinue()
		{
			logger.Info("DataLogger was sent CONTINUE signal.");
			main_timer.Start();
			base.OnContinue();
		}

		protected override void OnCustomCommand(int command)
		{
			logger.Info("DataLogger was sent custom signal:" + command.ToString());
			switch (command)
			{
				default:
					base.OnCustomCommand(command);
					break;
			}
		}

		public EnphaseLogger()
		{
			InitializeComponent();
		}
		#endregion
	}
}
