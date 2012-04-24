using System;
using System.Collections.Concurrent;
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
using System.Threading;
using NLog;
using System.Runtime.Remoting.Messaging;

namespace WaterShed.DataLogger
{
	public partial class DataLogger : ServiceBase
	{
		private MySqlConnection dbconn;
		private SerialPort sp;
        private String recvdString = String.Empty;

        private delegate void PacketProcessorCaller(string packet);
        private PacketProcessorCaller caller;
        // Used to make sure database writes have finished when we stop the service.
        private int numRunning = 0;

        /// <summary>
        /// This logger is used for general logging.
        /// </summary>
		private static Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// This logger is used specifically for logging invalid packets.  It is separate so that it can
        /// be more easily turned on and off as necessary.
        /// </summary>
		private static Logger logger2 = LogManager.GetLogger("DataPacket");

        /// <summary>
        /// This timer checks when the last packet came in, and restarts the service if
        /// it hasn't received a packet since the last tick.
        /// </summary>
        private System.Timers.Timer checkForRecentInput;
		private DateTime lastUpdate = DateTime.Now;
        private DateTime lastTick = DateTime.Now;
		
		private void AppStart(string[] args)
		{
			logger.Info("DataLogger Starting up.");

            #region Try to Open Database
            dbconn = new MySqlConnection(DB.GetDBConn());
			try
			{
				dbconn.Open();
			}
			catch (Exception ex)
			{
				logger.ErrorException("Failed to Open Database Connection.", ex);
				throw ex; //need to put in more error handling
            }
            #endregion

            #region Set up Timer to make sure input is still coming in
            checkForRecentInput = new System.Timers.Timer(Settings.Default.RecentInputTimer_Interval);
            checkForRecentInput.Elapsed += new System.Timers.ElapsedEventHandler(checkForRecentInput_Elapsed);
            #endregion

            #region Set up callback for processing received packets asynchroniously
            caller = new PacketProcessorCaller(processPacketAsync);
            #endregion

            #region Set up and try to open serial port
            sp = new SerialPort(Settings.Default.COMPort, Settings.Default.BaudRate,
                Settings.Default.Parity, Settings.Default.DataBits, Settings.Default.StopBits);
            sp.DataReceived += new SerialDataReceivedEventHandler(sp_DataReceived);

            try
			{
				if (sp.IsOpen)
				{
					//I can't imagine why this would already be the case... but we do it for safety.
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
            #endregion

            logger.Info("DataLogger Started Sucessfully!");
		}

        /// <summary>
        /// This method id fired when the serial port receives data.
        /// It can be any number of bytes so we have to use <code>SerialPort.BytesToRead()</code>.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// No need to worry about a data race because the event <code>lock</code>s on the
        /// <code>SerialStream</code> that underlies the <code>SerialPort</code>.  See
        /// http://social.msdn.microsoft.com/Forums/en-US/netfxbcl/thread/e36193cd-a708-42b3-86b7-adff82b19e5e/
        /// </remarks>
        void sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            while (sp.BytesToRead > 0)
            {
                char dataChar = (char)sp.ReadChar();

                // if we have reached the end of a packet
                if (recvdString.IndexOf("<< ") >= 0)
                {
                    // The next three characters are check digits for this packet.
                    for (int i = 0; i < 3; i++)
                    {
                        recvdString += (char)sp.ReadChar();
                    }

                    // Send the complete packet to be processed
                    //caller.BeginInvoke(String.Copy(recvdString), null, null);
                    caller.BeginInvoke(String.Copy(recvdString), CallBackMethodForDelegate, null);
                    // clear out the receive buffer to get the next packet
                    recvdString = "";
                }
                else
                {
                    recvdString += dataChar;
                }
            }
        }

        /// <summary>
        /// Check whether we have received packet(s) since the last tick.
        /// If we haven't, restart the DataLogger.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// Called by <see cref="ElapsedEventHandler.ElapsedEventHandler"/>.
        /// </remarks>
        void checkForRecentInput_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (lastUpdate < lastTick)
            {
                logger.Info("No Data in last two minutes.  Attempting service restart.");
                try
                {
                    sp.Close();
                }
                finally { }

                sp = null;

                Environment.Exit(1); //This makes the service manager see a crashed service process and restart it.
            }
            lastTick = e.SignalTime;
        }

        /// <summary>
        /// Process a complete packet received from the DataLogger system.
        /// Check that it is valid and then store its payload in the database.
        /// </summary>
        /// <param name="dataLine">A string containing the packet</param>
        void processPacketAsync(string dataLine)
        {
            logger.Trace("Received Complete Packet.");

            // Prevent service from stopping while we are processing a packet.
            Interlocked.Increment(ref numRunning);

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

        public void CallBackMethodForDelegate(IAsyncResult result)
        {
            PacketProcessorCaller caller = (PacketProcessorCaller)((AsyncResult)result).AsyncDelegate;
            caller.EndInvoke(result);
            Interlocked.Decrement(ref numRunning);
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
            sp.DataReceived -= sp_DataReceived;
            while (numRunning > 0)
            {
                // Wait for database writes in progress to finish
                Thread.Sleep(50);
            }
			try
			{
				dbconn.Close();
			}
			finally { }
		}

		protected override void OnPause()
		{
			logger.Info("DataLogger was sent PAUSE signal.");
            sp.DataReceived -= sp_DataReceived;
			base.OnPause();
		}

		protected override void OnContinue()
		{
			logger.Info("DataLogger was sent CONTINUE signal.");
            sp.DataReceived += new SerialDataReceivedEventHandler(sp_DataReceived);
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
