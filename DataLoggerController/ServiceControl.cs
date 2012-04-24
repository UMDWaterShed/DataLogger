using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.ServiceProcess;
using WaterShed.DataLogger;

namespace DataLoggerController
{
	public partial class ServiceControl : Form
	{
		private ServiceController sc1, sc2, sc3;

		public ServiceControl()
		{
			InitializeComponent();

			sc1 = new ServiceController("WaterShedDataLogger");
			sc2 = new ServiceController("WaterShedDataToWeb");
			sc3 = new ServiceController("WaterShedEnphaseLogger");

			try
			{
				sc1.Refresh();
			}
			catch (InvalidOperationException ex)
			{
				MessageBox.Show("Unable to find service: WaterShedDataLogger\n\nException: " + ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
				//using Environment so it does not show the form.
				Environment.Exit(1);
			}

			try
			{
				sc2.Refresh();
			}
			catch (InvalidOperationException ex)
			{
				MessageBox.Show("Unable to find service: WaterShedDataToWeb\n\nException: " + ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
				//using Environment so it does not show the form.
				Environment.Exit(1);
			}

			try
			{
				sc3.Refresh();
			}
			catch (InvalidOperationException ex)
			{
				MessageBox.Show("Unable to find service: WaterShedEnphaseLogger\n\nException: " + ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
				//using Environment so it does not show the form.
				Environment.Exit(1);
			}

			UpdateServiceStatus();
			timer1.Start();
		}

		private void UpdateServiceStatus()
		{
			switch (sc1.Status)
			{
				case ServiceControllerStatus.Paused:
					button1.Enabled = true;
					button2.Enabled = false;
					break;
				case ServiceControllerStatus.Running:
					button1.Enabled = false;
					button2.Enabled = true;
					pictureBox1.Image = Properties.Resources.play;
					break;
				case ServiceControllerStatus.PausePending:
				case ServiceControllerStatus.ContinuePending:
				case ServiceControllerStatus.StartPending:
				case ServiceControllerStatus.StopPending:
					button1.Enabled = false;
					button2.Enabled = false;
					break;
				case ServiceControllerStatus.Stopped:
					button1.Enabled = true;
					button2.Enabled = false;
					pictureBox1.Image = Properties.Resources.stop;
					break;
				default:
					break;
			}

			switch (sc2.Status)
			{
				case ServiceControllerStatus.ContinuePending:
				case ServiceControllerStatus.PausePending:
				case ServiceControllerStatus.Paused:
				case ServiceControllerStatus.StartPending:
				case ServiceControllerStatus.StopPending:
					button6.Enabled = true;
					button5.Enabled = false;
					break;
				case ServiceControllerStatus.Running:
					button6.Enabled = false;
					button5.Enabled = true;
					pictureBox2.Image = Properties.Resources.play;
					break;
				case ServiceControllerStatus.Stopped:
					button6.Enabled = true;
					button5.Enabled = false;
					pictureBox2.Image = Properties.Resources.stop;
					break;
				default:
					break;
			}

			switch (sc3.Status)
			{
				case ServiceControllerStatus.ContinuePending:
				case ServiceControllerStatus.PausePending:
				case ServiceControllerStatus.Paused:
				case ServiceControllerStatus.StartPending:
				case ServiceControllerStatus.StopPending:
					button8.Enabled = true;
					button7.Enabled = false;
					break;
				case ServiceControllerStatus.Running:
					button8.Enabled = false;
					button7.Enabled = true;
					pictureBox3.Image = Properties.Resources.play;
					break;
				case ServiceControllerStatus.Stopped:
					button8.Enabled = true;
					button7.Enabled = false;
					pictureBox3.Image = Properties.Resources.stop;
					break;
				default:
					break;
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			sc1.Start();
		}

		private void timer1_Tick(object sender, EventArgs e)
		{
			sc1.Refresh();
			sc2.Refresh();
			sc3.Refresh();
			UpdateServiceStatus();
		}

		private void button2_Click(object sender, EventArgs e)
		{
			sc1.Stop();
		}

		private void button6_Click(object sender, EventArgs e)
		{
			sc2.Start();
		}

		private void button5_Click(object sender, EventArgs e)
		{
			sc2.Stop();
		}

		private void button8_Click(object sender, EventArgs e)
		{
			sc3.Start();
		}

		private void button7_Click(object sender, EventArgs e)
		{
			sc3.Stop();
		}
	}
}
