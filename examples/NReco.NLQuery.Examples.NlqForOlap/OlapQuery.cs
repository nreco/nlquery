using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NReco.NLQuery.Table;

namespace NReco.NLQuery.Examples.NlqForOlap {

	/// <summary>
	/// This is representation of OLAP query.
	/// In real-world application this model can be used for generation of MDX or SQL GROUP BY query.
	/// </summary>
	public class OlapQuery {

		public string[] Dimensions { get; set; }
		public string[] Measures { get; set; }

		public ColumnCondition[] Filters { get; set; }

		public class ColumnCondition {
			public ColumnSchema Column { get; set; }
			public ColumnConditionMatch.ConditionType Condition { get; set; }
			public string Value { get; set; }

			public override string ToString() {
				return $"({Column.Name} {Condition} {Value})";
			}
		}

		public override string ToString() {
			var sb = new StringBuilder();
			sb.AppendLine("OLAP query:");
			sb.AppendLine("\tDimensions: "+String.Join(" ", Dimensions) );
			sb.AppendLine("\tMeasures: " + String.Join(" ", Measures));
			sb.AppendLine("\tFilters: " + String.Join(" ", Filters.Select(f=>f.ToString()).ToArray() ));

			return sb.ToString();
		}
	}


}
