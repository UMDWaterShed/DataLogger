using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnphaseParser
{
	public class EnphaseProduction
	{

		/// <summary>
		/// Gets the uptime for the system
		/// </summary>
		public DateTime Uptime
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the current production of the array in watts
		/// </summary>
		public double Current
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the production from today in watts
		/// </summary>
		public double Today
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the production from teh past week in watts
		/// </summary>
		public double PastWeek
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the production since installation of the array in watts
		/// </summary>
		public double SinceInstallation
		{
			get;
			private set;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="_uptime"></param>
		/// <param name="_current"></param>
		/// <param name="_today"></param>
		/// <param name="_pastWeek"></param>
		/// <param name="_sinceInstallation"></param>
		public EnphaseProduction(DateTime _uptime, double _current, double _today, double _pastWeek, double _sinceInstallation)
		{
			this.Uptime = _uptime;
			this.Current = _current;
			this.Today = _today;
			this.PastWeek = _pastWeek;
			this.SinceInstallation = _sinceInstallation;
		}

	}
}
