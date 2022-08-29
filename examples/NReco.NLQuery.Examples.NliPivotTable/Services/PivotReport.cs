using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using NReco.PivotData;

namespace NReco.NLQuery.Examples.NliPivotTable {

	/// <summary>
	/// Represents pivot table report configuration.
	/// </summary>
	public class PivotReport : PivotTableConfiguration {
		public string Filter { get; set; }
	}
}