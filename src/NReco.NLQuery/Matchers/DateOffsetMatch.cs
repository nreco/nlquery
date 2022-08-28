/*
 *  Copyright 2016-2018 Vitaliy Fedorchenko (nrecosite.com)
 *
 *  Licensed under NLQuery Source Code License (see LICENSE file).
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS 
 *  OF ANY KIND, either express or implied.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NReco.NLQuery.Matchers {
	
	/// <summary>
	/// Represents a result of data offset match.
	/// </summary>
	public class DateOffsetMatch : Match {

		/// <summary>
		/// Day component of the date match (1-31).
		/// </summary>
		public int? Day { get; set; }

		/// <summary>
		/// Month component of the date match (1-12).
		/// </summary>
		public int? Month { get; set; }

		/// <summary>
		/// Year component of the date match (4-digit).
		/// </summary>
		public int? Year { get; set; }

		public DateOffsetMatch()  {

		}

		public DateMatch ToDate(DateTime baseDt) {
			var date = new DateMatch();
			date.Score = Score;
			date.Start = Start;
			date.End = End;

			bool copyYear = false;
			bool copyMonth = false;
			bool copyDay = false;
			if (Year.HasValue) {
				baseDt = baseDt.AddYears(Year.Value);
				copyYear = true;
			}
			if (Month.HasValue) {
				baseDt = baseDt.AddMonths(Month.Value);
				copyYear = copyMonth = true;
			}
			if (Day.HasValue) {
				baseDt = baseDt.AddDays(Month.Value);
				copyYear = copyMonth = copyDay = true;
			}

			if (copyYear) date.Year = baseDt.Year;
			if (copyMonth) date.Month = baseDt.Month;
			if (copyDay) date.Day = baseDt.Day;

			return date;
		}

		public override string ToString() {
			var sb = new StringBuilder();
			if (Year.HasValue)
				sb.AppendFormat("Y:{0}", Year.Value);
			if (Month.HasValue) { 
				if (sb.Length>1)
					sb.Append(' ');
				sb.AppendFormat("M:{0}", Month.Value);
			}
			if (Day.HasValue) { 
				if (sb.Length>1)
					sb.Append(' ');	
				sb.AppendFormat("D:{0}", Day.Value);
			}
			return "DateOffset["+sb.ToString()+"]";
		}
	}

	
}
