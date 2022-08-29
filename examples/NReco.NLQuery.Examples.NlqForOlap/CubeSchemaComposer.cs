using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NReco.NLQuery.Table;

namespace NReco.NLQuery.Examples.NlqForOlap {

	public class CubeSchemaComposer {

		public TableSchema GetSchema() {
			// in this stub implementation cube schema is hardcoded
			// in real-life usage it should be composed by cube metadata (dimensions/measures)

			var columns = new List<ColumnSchema>();
			AddDimensions(columns);
			AddMeasures(columns);

			var tblSchema = new TableSchema() {
				Columns = columns.ToArray()
			};

			return tblSchema;
		}

		void AddDimensions(List<ColumnSchema> columns) {

			// this column can be matched by values
			columns.Add(new ColumnSchema() {
				Name = "[employee_name]",
				Caption = "Name",
				DataType = TableColumnDataType.String,
				Values = new[] {"John Smith", "Mary Kinsey", "John Snow"}
			});

			// this column can be included into query only if present in the NLQ with "rank" keyword
			columns.Add(new ColumnSchema() {
				Name = "[rank]",
				Caption = "Rank",
				DataType = TableColumnDataType.Number
			});

			columns.Add(new ColumnSchema() {
				Name = "[region]",
				Caption = "Region",
				DataType = TableColumnDataType.String,
				Values = new [] {"Europe", "Asia", "North America", "Africa"}
			});
		}

		void AddMeasures(List<ColumnSchema> columns) {
			// for measures 'CubeMeasureAsColumnSchema' is used as we need to separate them 
			// from columns that correspond to cube dimensions (in recognition results processing)

			columns.Add(new CubeMeasureAsColumnSchema() {
				Name = "[Measures].[Count]",
				Caption = "Count",
				AggregateFunction = "COUNT",
				DataType = TableColumnDataType.Number
			});

			columns.Add(new CubeMeasureAsColumnSchema() {
				Name = "[Measures].[Sum of Sales]",
				Caption = "Sales Sum", // if possible, better to avoid stop-words like 'of' in captions that are used for matching
				AggregateFunction = "COUNT",
				DataType = TableColumnDataType.Number
			});

		}
	}

	// represents measure as column
	public class CubeMeasureAsColumnSchema : ColumnSchema {

		// you can add any extra properties that you need for formal OLAP query generation
		// in this example, lets include name of the aggregate function
		// it is not really used as 'Name' corresponds to MDX member name

		public string AggregateFunction { get; set; }
	}

}
