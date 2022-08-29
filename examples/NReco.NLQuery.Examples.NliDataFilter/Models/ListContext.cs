using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using NReco.Data;

namespace NReco.NLQuery.Examples.NliDataFilter.Models {
	public class ListContext {

		public RecordSet Data { get; set; }

		public string FilterCondition { get; set; }

	}
}