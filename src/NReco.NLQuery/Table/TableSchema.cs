using System;
using System.Collections.Generic;
using System.Text;

namespace NReco.NLQuery.Table
{

	/// <summary>
	/// Describes a table (any tabular data) metadata.
	/// </summary>
	public class TableSchema {

		/// <summary>
		/// User-friendly table name that can be recognized in a search query.
		/// </summary>
		public string Caption { get; set; }

		/// <summary>
		/// Alternative table captions to match (optional).
		/// </summary>
		public string[] AltCaptions { get; set; }

		/// <summary>
		/// Internal table name (not used for recognition).
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// List of colums assosiated with this table.
		/// </summary>
		public ColumnSchema[] Columns { get; set; }

		/// <summary>
		/// Forces only exact match for the table caption.
		/// </summary>
		public bool ExactMatchOnly { get; set; } = false;

		internal IEnumerable<string> GetCaptionsToMatch() {
			if (Caption != null)
				yield return Caption;
			if (AltCaptions != null)
				foreach (var altCaption in AltCaptions)
					yield return altCaption;
		}
	}

	/// <summary>
	/// Describes a data column.
	/// </summary>
	public class ColumnSchema {

		/// <summary>
		/// User-friendly column name that can be recognized in a search query.
		/// </summary>
		public string Caption { get; set; }

		/// <summary>
		/// Alternative table captions to match (optional).
		/// </summary>
		public string[] AltCaptions { get; set; }

		/// <summary>
		/// Internal column name (not used for recognition).
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Column data type.
		/// </summary>
		public TableColumnDataType DataType { get; set; } = TableColumnDataType.String;

		/// <summary>
		/// List of unique values for this column (optional). Column can be determined by these values without a hint.
		/// </summary>
		public string[] Values { get; set; }

		/// <summary>
		/// Forces only exact match for the column caption.
		/// </summary>
		public bool ExactMatchOnly { get; set; } = false;

		internal IEnumerable<string> GetCaptionsToMatch() {
			if (Caption != null)
				yield return Caption;
			if (AltCaptions != null)
				foreach (var altCaption in AltCaptions)
					yield return altCaption;
		}
	}

	/// <summary>
	/// Data types that are significant for search query recognition.
	/// </summary>
	public enum TableColumnDataType {
		String = 0,
		Number = 1,
		Date = 2,
		Unknown = 255
	}

}
