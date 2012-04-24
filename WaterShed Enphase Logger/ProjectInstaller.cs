using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;


namespace WaterShed_Enphase_Logger
{
	[RunInstaller(true)]
	public partial class ProjectInstaller : System.Configuration.Install.Installer
	{
		public ProjectInstaller()
		{
			InitializeComponent();
		}
	}
}
