using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NReco.PivotData;
using NReco.NLQuery;
using NReco.NLQuery.Table;
using NReco.NLQuery.Matchers;

namespace NReco.NLQuery.Examples.NliPivotTable {

	/// <summary>
	/// Parses NLQ and returns top-matched <see cref="PivotReport"/> models.
	/// </summary>
	public class QueryParser {

		public string[] StopWords { get; set; }
		public Func<string, string> StemWord { get; set; }

		Recognizer ListQueryRecognizer;
		TableSchema TableSchema;

		public QueryParser(IPivotData pvtData) {
			StopWords = new[] {
				"a", "by", "an", "at", "are", "as", "be", "at", "do","does","did", "etc", "for", "has", "have", "had", "in", "is", "just", "near",
				"of", "on", "per", "the", "to", "vs", "versus", "x", "was",
				"how","many","much","if","it","its","up","so","out",
				"show","about","after",
				"me","i","am","he","his","she","her","any","all","they","their", "them","our","ours",
				"be","been","being","both","but","that","than","could",
				"and", "or", "from", "no", "not"
			};
			StemWord = new EnglishStemmer().Stem;
			TableSchema = ComposeTableSchema(pvtData);

			ListQueryRecognizer = GetRecognizer(TableSchema);
		}

		// configure NLQuery Recognizer by data table schema with help of TableMatcherBuilder
		Recognizer GetRecognizer(TableSchema tblSchema) {
			var tblMatchBuilder = new TableMatcherBuilder(new TableMatcherBuilder.Options() {
				StopWords = StopWords,
				MatchBoolOperators = false  // CubeKeywordFilter doesn't support and/or
			});
			tblMatchBuilder.Add(tblSchema);
			// use tblMatchBuilder.Add if you need to configure additional (custom) matchers
			return new Recognizer(tblMatchBuilder.Build());
		}

		public PivotReport[] Parse(string queryStr, int maxResults) {
			var tokenizer = new Tokenizer();
			var tokens = ApplyStemmer(tokenizer.Parse(queryStr)).ToArray();

			var statement = new TokenSequence(tokens);
			var topCandidates = new TopSet<QueryCandidate>(maxResults, (a, b) => a.Score.CompareTo(b.Score));

			int processedMatches = 0;
			ListQueryRecognizer.Recognize(statement,
				match => {
					// handle only matches that are signficant for the list filtering
					if (match is ColumnConditionMatch colCndMatch) {
						return !(colCndMatch.Value is StubMatch) || colCndMatch.Value.Start.Type == TokenType.Number;
					}
					return match is ColumnMatch;
				},
				(matches) => {
					topCandidates.Add(new QueryCandidate(matches, statement));
					processedMatches++;
					if (processedMatches > 1000) // some reasonable limit for number of combinations to process
						return false;
					return true;
				}
			);
			return topCandidates.ToArray().Select(candidate => candidate.ToPivotReport()).ToArray();
		}

		/// <summary>
		/// Suggest by prefix known keywords.
		/// </summary>
		public string[] SuggestKeywords(string term, int limit) {
			var t = new Token(TokenType.Word, 0, term);
			var tokenSeq = new TokenSequence(t, new Token(TokenType.SentenceEnd, term.Length, String.Empty));
			var topMatches = new TopSet<Match>(limit, (a, b) => a.Score.CompareTo(b.Score));
			int processed = 0;
			ListQueryRecognizer.Recognize(tokenSeq,
				m => m is ColumnMatch || (m is ColumnConditionMatch colCndMatch && colCndMatch.MatchedValue != null && colCndMatch.Condition != ColumnConditionMatch.ConditionType.Contains),
				(matches) => {
					processed++;
					if (matches.Length == 1) {
						topMatches.Add(matches[0]);
					}
					return processed < 1000;
				}
			);
			var matchedKeywords = new List<string>();
			foreach (var m in topMatches.ToArray())
				switch (m) {
					case ColumnMatch colMatch:
						matchedKeywords.Add(colMatch.Column.Caption);
						break;
					case ColumnConditionMatch colCndMatch:
						matchedKeywords.Add(colCndMatch.MatchedValue);
						break;
				}
			return matchedKeywords.ToArray();
		}

		// represents measure as column
		internal class CubeMeasureAsColumnSchema : ColumnSchema {
			internal int AggregatorIndex;
		}

		// represents date dimension as column
		internal class CubeDateDimensionAsColumnSchema : ColumnSchema {
			internal ColumnSchema YearDimension { get; set; }
			internal ColumnSchema MonthDimension { get; set; }
			internal ColumnSchema DayDimension { get; set; }
		}

		TableSchema ComposeTableSchema(IPivotData pvtData) {
			// TBD: dimensions need to have DataType (DbType) - they're already detected in infer schema
			var cols = new List<ColumnSchema>();
			var dimKeys = PivotDataHelper.GetDimensionKeys(pvtData, null, null);
			for (int dimIdx=0; dimIdx<pvtData.Dimensions.Length; dimIdx++) {
				var dim = pvtData.Dimensions[dimIdx];

				// as tmp solution date dims are determined by derived year/month/day
				var col = new ColumnSchema() { Caption = dim, Name = dim };
				var dateCol = TryAsDateDimension(dim);
				if (dateCol != null) {
					col = dateCol;
				} else {
					if (isNumber(PivotDataHelper.GetDimensionType(dimKeys[dimIdx]))) {
						col.DataType = TableColumnDataType.Number;
					} else {
						col.Values = dimKeys[dimIdx].Select(v => v.ToString()).ToArray();
					}
				}
				cols.Add(col);
			}
			var aggrs = pvtData.AggregatorFactory is CompositeAggregatorFactory composite ?
							composite.Factories : new[] { pvtData.AggregatorFactory };
			for (int aggrIdx=0; aggrIdx<aggrs.Length; aggrIdx++) {
				var aggr = aggrs[aggrIdx];
				var col = new CubeMeasureAsColumnSchema() {
					Caption = aggr.ToString(),
					AggregatorIndex = aggrIdx,
					DataType = TableColumnDataType.Number
				};
				cols.Add(col);
			}
			return new TableSchema() {
				Columns = cols.ToArray()
			};

			ColumnSchema TryAsDateDimension(string dim) {
				ColumnSchema yearDim = null;
				ColumnSchema monthDim = null;
				ColumnSchema dayDim = null;
				for (int i = 0; i < pvtData.Dimensions.Length; i++) {
					var d = pvtData.Dimensions[i];
					if (d.StartsWith(dim)) {
						if (yearDim == null && d.IndexOf("year", dim.Length, StringComparison.OrdinalIgnoreCase) >= 0)
							yearDim = new ColumnSchema() { Name = d, Caption = d };
						if (monthDim == null && d.IndexOf("month", dim.Length, StringComparison.OrdinalIgnoreCase) >= 0)
							monthDim = new ColumnSchema() { Name = d, Caption = d };
						if (dayDim == null && d.IndexOf("day", dim.Length, StringComparison.OrdinalIgnoreCase) >= 0)
							dayDim = new ColumnSchema() { Name = d, Caption = d };
					}
				}
				if (yearDim != null && monthDim != null && dayDim != null) {
					var col = new CubeDateDimensionAsColumnSchema();
					col.DataType = TableColumnDataType.Date;
					col.YearDimension = yearDim;
					col.MonthDimension = monthDim;
					col.DayDimension = dayDim;
					return col;
				}
				return null;
			}
			bool isNumber(Type t) {
				switch (Type.GetTypeCode(Nullable.GetUnderlyingType(t) ?? t)) {
					case TypeCode.Byte:
					case TypeCode.SByte:
					case TypeCode.UInt16:
					case TypeCode.UInt32:
					case TypeCode.UInt64:
					case TypeCode.Int16:
					case TypeCode.Int32:
					case TypeCode.Int64:
					case TypeCode.Decimal:
					case TypeCode.Double:
					case TypeCode.Single:
						return true;
				}
				return false;
			}
		}

		internal class QueryCandidate {

			public Match[] Matches { get; private set; }

			public float Score { get; private set; }

			public TokenSequence SearchQuery { get; private set; }

			public QueryCandidate(Match[] matches, TokenSequence searchQuery) {
				Matches = matches;
				SearchQuery = searchQuery;

				// this is example of scoring function 
				// sum of all matches weighted by number of matched words or numbers
				// it can be enhanced to be more precise: say, you can boost scores for some dimensions/measures

				var totalWordOrNumCount = searchQuery.Tokens.Where(t => t.Type == TokenType.Word || t.Type == TokenType.Number).Count();
				float totalScore = 0f;
				int totalMatchedWordOrNumCount = 0;
				foreach (var m in matches) {
					var matchedWordOrNumCount = wordOrNumCount(m);
					totalMatchedWordOrNumCount += matchedWordOrNumCount;
					totalScore += m.Score * ((float)matchedWordOrNumCount) / totalWordOrNumCount;
				}
				// boost for 'long' matches
				if (totalMatchedWordOrNumCount > 0)
					totalScore += 0.3f * (1f - ((float)matches.Length) / totalMatchedWordOrNumCount);
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

			public PivotReport ToPivotReport() {
				var dimToFltCnt = new Dictionary<string, int>();
				var dims = new List<string>();
				var filters = new List<string>();
				var measures = new List<int>();
				for (int i = (Matches.Length - 1); i >= 0; i--) {
					var m = Matches[i];
					switch (m) {
						case ColumnMatch colMatch:
							if (colMatch.Column is CubeMeasureAsColumnSchema measureCol) {
								addMeasure(measureCol.AggregatorIndex);
							} else {
								addDim(colMatch.Column.Name, 0);
							}
							break;
						case ColumnConditionMatch colCndMatch:
							if (colCndMatch.Column is CubeDateDimensionAsColumnSchema) {
								addDatePartFilter(colCndMatch);
							} else {
								var withFilter = colCndMatch.Score > 0;
								if (withFilter)
									addFilter(colCndMatch, filters);
								else
									addDim(colCndMatch.Column.Name, 0);
							}
							break;
					}
				}
				var rowDims = new List<string>();
				var colDims = new List<string>();
				// prefer dims with filters for columns
				foreach (var entry in dimToFltCnt.Where(entry => entry.Value > 0).OrderBy(entry => entry.Value)) {
					if (colDims.Count >= (dims.Count / 2))
						break;
					colDims.Add(entry.Key);
					dims[dims.IndexOf(entry.Key)] = null; // exclude from processing
				}
				foreach (var dim in dims) {
					if (dim == null)
						continue;
					if (rowDims.Count <= (dims.Count / 2)) {
						rowDims.Add(dim);
					} else {
						colDims.Add(dim);
					}
				}
				return new PivotReport() {
					Columns = colDims.ToArray(),
					Rows = rowDims.ToArray(),
					Measures = measures.ToArray(),
					Filter = String.Join(", ", filters.ToArray())
				};

				void addDim(string dim, int filterCount) {
					if (!dimToFltCnt.ContainsKey(dim)) {
						dimToFltCnt[dim] = 0;
						dims.Add(dim);
					}
					dimToFltCnt[dim] += filterCount;
				}
				void addMeasure(int aggrIdx) {
					if (!measures.Contains(aggrIdx))
						measures.Add(aggrIdx);
				}
				void addDatePartFilter(ColumnConditionMatch colCndMatch) {
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
				}

				void addFilter(ColumnConditionMatch colCndMatch, List<string> filterList) {
					if (colCndMatch.Value is DateMatch || colCndMatch.Value is DateOffsetMatch) {
						//??
					}
					if (colCndMatch.Column is CubeMeasureAsColumnSchema colMeasure) {
						addMeasure(colMeasure.AggregatorIndex);
					} else {
						addDim(colCndMatch.Column.Name, 1);
					}
					var val = String.Concat( SearchQuery.Between(colCndMatch.Value.Start, colCndMatch.Value.End, true).Select(t=>t.Value) );
					var colHint = colCndMatch.Column.Caption;
					if (!isOnlyAlphaNum(colHint))
						colHint = "\"" + colHint + "\"";
					if (!isOnlyAlphaNum(val))
						val = "\"" + val + "\"";
					switch (colCndMatch.Condition) {
						case ColumnConditionMatch.ConditionType.Exact:
							filterList.Add($"{colHint}:{val}");
							break;
						case ColumnConditionMatch.ConditionType.StartsWith:
							filterList.Add($"{colHint}:{val}*");
							break;
						case ColumnConditionMatch.ConditionType.Contains:
						case ColumnConditionMatch.ConditionType.Like:
							filterList.Add($"{colHint}:*{val}*");
							break;
						case ColumnConditionMatch.ConditionType.Equal:
							filterList.Add($"{colHint}={val}");
							break;
						case ColumnConditionMatch.ConditionType.NotEqual:
							filterList.Add($"{colHint}<>{val}");
							break;
						case ColumnConditionMatch.ConditionType.LessThan:
							filterList.Add($"{colHint}<{val}");
							break;
						case ColumnConditionMatch.ConditionType.LessThanOrEqual:
							filterList.Add($"{colHint}<={val}");
							break;
						case ColumnConditionMatch.ConditionType.GreaterThan:
							filterList.Add($"{colHint}>{val}");
							break;
						case ColumnConditionMatch.ConditionType.GreaterThanOrEqual:
							filterList.Add($"{colHint}>={val}");
							break;
					}

				}
				bool isOnlyAlphaNum(string s) {
					for (int i = 0; i < s.Length; i++) {
						var ch = s[i];
						if (!Char.IsLetterOrDigit(ch) && ch != '_')
							return false;
					}
					return true;
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

		// for better matching stem words
		IEnumerable<Token> ApplyStemmer(IEnumerable<Token> tokens) {
			foreach (var t in tokens) {
				if (t.Type == TokenType.Word) {
					var stemmedVal = StemWord(t.Value);
					if (stemmedVal != t.Value) {
						yield return new Token(t.Type, t.StartIndex, stemmedVal);
						continue;
					}
				}
				yield return t;
			}
		}


	}

}
