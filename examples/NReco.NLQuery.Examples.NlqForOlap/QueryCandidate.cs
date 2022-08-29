using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NReco.NLQuery.Matchers;
using NReco.NLQuery.Table;

namespace NReco.NLQuery.Examples.NlqForOlap {

	/// <summary>
	/// Represents combination of matches with total score that can be converted to formal OLAP query.
	/// </summary>
	public class QueryCandidate {

		public Match[] Matches { get; private set; }

		public float Score { get; private set; }

		public TokenSequence SearchQuery { get; private set; }

		public QueryCandidate(Match[] matches, TokenSequence searchQuery) {
			Matches = matches;
			SearchQuery = searchQuery;

			// in this example simple scoring function is used:
			// sum of all matches weighted by number of matched words or numbers
			// you can customize it for better recognition of your concrete cube(say, boost scores for some dimensions/measures)

			var totalWordOrNumCount = searchQuery.Tokens.Where(t => t.Type == TokenType.Word || t.Type == TokenType.Number).Count();
			float totalScore = 0f;
			foreach (var m in matches) {
				totalScore += m.Score * ((float)wordOrNumCount(m)) / totalWordOrNumCount;
			}
			Score = totalScore;

			int wordOrNumCount(Match m) {
				var startTokenIdx = searchQuery.GetIndex(m.Start);
				var endTokenIdx = searchQuery.GetIndex(m.End);
				int cnt = 0;
				Token t;
				for (var i = startTokenIdx; i <= endTokenIdx; i++) {
					t = searchQuery.Tokens[i];
					if (t.Type == TokenType.Word || t.Type == TokenType.Number)
						cnt++;
				}
				return cnt;
			}
		}

		/// <summary>
		/// Create formal query by matches in this candidate.
		/// </summary>
		public OlapQuery ToOlapQuery() {
			var dimToFltCnt = new Dictionary<string, int>();

			var dims = new List<string>();
			var measures = new List<string>();
			var filters = new List<OlapQuery.ColumnCondition>();

			for (int i = (Matches.Length - 1); i >= 0; i--) {
				var m = Matches[i];
				switch (m) {
					case ColumnMatch colMatch:
						if (colMatch.Column is CubeMeasureAsColumnSchema measureCol) {
							if (!measures.Contains(measureCol.Name))
								measures.Add(measureCol.Name);
						} else {
							addDim(colMatch.Column.Name, 0);
						}
						break;
					case ColumnConditionMatch colCndMatch:
						var withFilter = colCndMatch.Score > 0;
						addDim(colCndMatch.Column.Name, withFilter ? 1 : 0);
						if (withFilter)
								addFilter(colCndMatch);
						break;
				}
			}
			return new OlapQuery() {
				Dimensions = dims.ToArray(),
				Measures = measures.ToArray(),
				Filters = filters.ToArray()
			};

			void addDim(string dim, int filterCount) {
				if (!dimToFltCnt.ContainsKey(dim)) {
					dimToFltCnt[dim] = 0;
					dims.Add(dim);
				}
				dimToFltCnt[dim] += filterCount;
			}
			/*void addDatePartFilter(ColumnConditionMatch colCndMatch) {
				var dateCol = colCndMatch.Column as CubeDateDimensionAsColumnSchema;
				var dateMatch = colCndMatch.Value as DateMatch;
				if (colCndMatch.Value is DateOffsetMatch)
					dateMatch = ((DateOffsetMatch)colCndMatch.Value).ToDate(DateTime.Now);
				if (dateMatch == null)
					return;
				if (dateMatch.Year.HasValue) {
					addDim(dateCol.YearDimension.Name, 1);
					filters.Add($"{dateCol.YearDimension.Caption}:{dateMatch.Year.Value}");
				}
				if (dateMatch.Month.HasValue) {
					addDim(dateCol.MonthDimension.Name, 1);
					filters.Add($"{dateCol.MonthDimension.Caption}:{dateMatch.Month.Value}");
				}
				if (dateMatch.Day.HasValue) {
					addDim(dateCol.DayDimension.Name, 1);
					filters.Add($"{dateCol.DayDimension.Caption}:{dateMatch.Day.Value}");
				}
			}*/

			void addFilter(ColumnConditionMatch colCndMatch) {
				if (colCndMatch.Value is DateMatch || colCndMatch.Value is DateOffsetMatch) {
					//??
				}
				var val = String.Concat(SearchQuery.Between(colCndMatch.Value.Start, colCndMatch.Value.End, true).Select(t => t.Value));
				filters.Add(new OlapQuery.ColumnCondition() {
					Column = colCndMatch.Column,
					Condition = colCndMatch.Condition,
					Value = val
				});
			}
		}

		public override string ToString() {
			var sb = new System.Text.StringBuilder();
			sb.AppendFormat("QueryCandidate: score={0}\n", Score);
			foreach (var m in Matches) {
				var joinedTokens = String.Join("|", SearchQuery.Between(m.Start, m.End).Select(t => t.Value).ToArray());
				sb.Append(String.Format("\t{0} score={1} tokens={2}]\n", m.ToString(), m.Score, joinedTokens));
			}
			return sb.ToString();
		}
	}
}
