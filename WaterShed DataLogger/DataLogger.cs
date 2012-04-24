using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using MySql.Data.MySqlClient;
using WaterShed.DataLogger.Properties;
using System.Text.RegularExpressions;
using System.IO;
using System.Timers;
using NLog;

namespace WaterShed.DataLogger
{
	public partial class DataLogger : ServiceBase
	{
		private MySqlConnection dbconn;
		SerialPort sp;

		private static Logger logger = LogManager.GetCurrentClassLogger();
		private static Logger logger2 = LogManager.GetLogger("DataPacket");

		private Timer main_timer;

		private DateTime lastUpdate = DateTime.Now;
		
		private void AppStart(string[] args)
		{
			main_timer = new Timer();

			logger.Info("DataLogger Starting up.");
			
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

			sp = new SerialPort(Settings.Default.COMPort, 19200,Parity.None,8,StopBits.One);

			try
			{
				if (sp.IsOpen)
				{
					//Reset it fresh
					sp.Close();
				}
				sp.Open();
				logger.Debug("Successfully opened " + Settings.Default.COMPort);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Could not open Serial Port.", ex);
				throw ex;
			}

			main_timer.Elapsed += new ElapsedEventHandler(main_timer_Tick);
			main_timer.Interval = Settings.Default.mainTimer_Interval;
			main_timer.Start();
		}

		private bool isInTick = false;

		private void main_timer_Tick(object sender, EventArgs e)
		{
			String recvdString = "";
			char dataChar;

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

			//DateTime duration = lastUpdate.AddMinutes(2);
			if (lastUpdate.AddMinutes(2) < DateTime.Now)
			{
				//logger.Info("No Data in last two minutes.  Attempting reconnection to serial port.");
				logger.Info("No Data in last two minutes.  Attempting service restart.");
				try
				{
					sp.Close();
				}
				finally { }
				
				main_timer.Stop();
				sp = null;
				System.Threading.Thread.Sleep(10 * 1000);

				Environment.Exit(1); //This makes the service manager see a crashed service process and restart it.

				//sp = new SerialPort(Settings.Default.COMPort, 19200, Parity.None, 8, StopBits.One);
				//try
				//{
				//	sp.Open();
				//}
				//catch (Exception ex)
				//{
				//	logger.ErrorException("Could not re-open Serial Port.", ex);
				//
				//	duration.AddMinutes(13);
				//	if (duration < DateTime.Now)
				//	{
				//		logger.Info("No data in last fifteen minutes.  Attempting System restart.");
				//		Process.Start("shutdown", "/r /t 0");
				//	}
				//
				//	//Don't throw - instead wait until next tick and try again.
				//	//throw ex;
				//}
				//main_timer.Start();
			}

			try
			{
				if (sp.IsOpen)
				{
					while (sp.BytesToRead > 0)
					{
						//dataChar = Convert.ToChar(sp.ReadByte()).ToString();
						//dataChar = (byte)sp.ReadByte();
						dataChar = Microsoft.VisualBasic.Strings.Chr(sp.ReadChar());

						Console.Write(dataChar);

						if (recvdString.IndexOf("<< ") >= 0)
						{
							for (int i = 0; i < 3; i++)
							{
								recvdString += Microsoft.VisualBasic.Strings.Chr(sp.ReadChar());
							}

							processPacket(recvdString.ToString());
							recvdString = "";
						}
						else
						{
							recvdString += dataChar;
						}
					}
				}
				else
				{
					try
					{
						logger.Warn("Port not open on Tick.  Trying to Open");
						sp.Open();
					}
					catch (Exception ex)
					{
						logger.ErrorException("Could not re-open Serial Port.", ex);
						//throw ex;
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Some Kind of Error Occured.", ex);

				sp.Close();
				throw ex;
			}
			finally
			{
				// allow the next tick to start
				isInTick = false;
			}
		}

		public void processPacket(string dataLine)
		{
			logger.Trace("Received Complete Packet.");
			try
			{
				int endPosition = dataLine.IndexOf('<');

				int checksum = 0;
				try
				{
					checksum = int.Parse(dataLine.Substring(endPosition + 1).Split()[1]);
				}
				catch (Exception)
				{
					logger.Debug("Invalid Checksum.  Discarding Packet. (Checksum not number or not present");
					logger2.Debug(dataLine);
					// A missing checksum invalidates the packet so we skip it and move on.
					return;
				}

				string regex = @"^(\s*>>.*<<\s*)";
				RegexOptions options = RegexOptions.Singleline;
				Match m = Regex.Match(dataLine, regex, options);

				string packet = m.ToString();

				int checksumCounter = 0;
				foreach (char c in packet)
				{
					checksumCounter ^= c;
				}

				if (checksumCounter != checksum)
				{
					logger.Debug("Incorrect Checksum.  Discarding Packet. (Not match)");
					logger2.Debug(dataLine);
					return; //ignore broken packet
				}

				char[] sep = { ' ' };
				string[] packetPieces = packet.Split(sep, 7,
					StringSplitOptions.RemoveEmptyEntries);

				if (packetPieces[1] != "STAT")
					return; // not for us

				String[] sub_types = { "SENSOR_VALUE", "RELAY_STATE", "CTRL_VALUE" };
				if (!sub_types.Contains(packetPieces[5]))
					return; // not for us

				int length = int.Parse(packetPieces[4]);

				packetPieces[6] = packetPieces[6].Substring(0, length)
					.Replace(':', ' ').Replace('|', ' ');

				string[] dataPoints = packetPieces[6].Split(sep, StringSplitOptions.RemoveEmptyEntries);

				int i = 2;
				while (i < dataPoints.Length)
				{
					(new DataPoint(dataPoints[1], dataPoints[i++], dataPoints[i++])).Save(dbconn);
					
					lastUpdate = DateTime.Now;
				}
			}
			catch (Exception ex)
			{
				logger.InfoException("Error Processing Packet", ex);
				return;
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

		public DataLogger()
		{
			InitializeComponent();
		}
		#endregion
	}
}
