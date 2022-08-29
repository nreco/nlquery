using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.IO;

using NReco.Data;

namespace NReco.NLQuery.Examples.NliDataFilter.Data {

	/// <summary>
	/// Loads list data with help of open-source NReco.Data library https://github.com/nreco/data
	/// (you can replace it with Dapper or EF)
	/// </summary>
	public class ListDataRepository {

		DbDataAdapter DbAdapter;

		public ListDataRepository(DbDataAdapter dbAdapter) {
			DbAdapter = dbAdapter;
		}

		public IList<string> LoadDistinctValues(string tableName, string columnName) {
			var q = new Query(tableName);
			q.Select( new QField(columnName, "DISTINCT "+columnName ) );
			return DbAdapter.Select(q).ToList<string>();
		}


		public RecordSet Load(Query q) {
			return DbAdapter.Select(q).ToRecordSet();
		}

	}

}