using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Data;
using NReco.Data;
using NReco.NLQuery;
using NReco.NLQuery.Table;
using NReco.NLQuery.Matchers;

namespace NReco.NLQuery.Examples.NliDataFilter.Data {
	
	/// <summary>
	/// Parses NLQ and returns top-matched queries to the data list.
	/// </summary>
	public class ListQueryParser {

		public string[] StopWords { get; set; }
		public Func<string, string> StemWord { get; set; }

		Recognizer ListQueryRecognizer;
		TableSchema TableSchema;

		public ListQueryParser(TableSchema tableSchema) {
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
			TableSchema = tableSchema;

			ListQueryRecognizer = GetRecognizer(TableSchema);
		}

		// configure NLQuery Recognizer by data table schema with help of TableMatcherBuilder
		Recognizer GetRecognizer(TableSchema tblSchema) {
			var tblMatchBuilder = new TableMatcherBuilder(new TableMatcherBuilder.Options() {
				StopWords = StopWords
			});
			tblMatchBuilder.Add(tblSchema);
			// use tblMatchBuilder.Add if you need to configure additional (custom) matchers
			return new Recognizer(tblMatchBuilder.Build());
		}

		public Query[] Parse(string queryStr, int maxResults) {
			var tokenizer = new Tokenizer();
			var tokens = ApplyStemmer(tokenizer.Parse(queryStr)).ToArray();

			var statement = new TokenSequence(tokens);
			var topCandidates = new TopSet<ListQueryCandidate>(maxResults, (a, b) => a.Score.CompareTo(b.Score));

			int processedMatches = 0;
			ListQueryRecognizer.Recognize(statement,
				match => {
					// handle only matches that are signficant for the list filtering
					if (match is ColumnConditionMatch colCndMatch) {
						return !(colCndMatch.Value is StubMatch) || colCndMatch.Value.Start.Type == TokenType.Number;
					}
					return match is ColumnMatch || match is GroupMatch;
				},
				(matches) => {
					topCandidates.Add(new ListQueryCandidate(matches, statement));
					processedMatches++;
					if (processedMatches > 1000) // some reasonable limit for number of combinations to process
						return false;
					return true;
				}
			);
			return topCandidates.ToArray().Select(candidate => candidate.ToQuery(TableSchema)).ToArray();
		}

		/// <summary>
		/// Suggests keywords that could be recognized in a query.
		/// </summary>
		public string[] SuggestKeywords(string term, int limit) {
			var t = new Token(TokenType.Word, 0, term);
			var tokenSeq = new TokenSequence(t, new Token(TokenType.SentenceEnd, term.Length, String.Empty));
			var topMatches = new TopSet<Match>(limit, (a, b) => a.Score.CompareTo(b.Score));
			int processed = 0;
			ListQueryRecognizer.Recognize(tokenSeq,
				m => m is ColumnMatch || (m is ColumnConditionMatch colCndMatch && colCndMatch.MatchedValue!=null && colCndMatch.Condition!=ColumnConditionMatch.ConditionType.Contains),
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

		public class ListQueryCandidate {

			public Match[] Matches { get; private set; }

			public float Score { get; private set; }

			public TokenSequence SearchQuery { get; private set; }

			public ListQueryCandidate(Match[] matches, TokenSequence searchQuery) {
				Matches = matches;
				SearchQuery = searchQuery;

				// this is example of scoring function 
				// sum of all matches weighted by number of matched words or numbers
				// it can be enhanced to be more precise: say, you can boost scores for some columns

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

			public Query ToQuery(TableSchema tblSchema) {
				// in this example columns to display are hardcoded
				// only filter is composed from the search query
				var q = new Query(tblSchema.Name);
				var andCnd = new QGroupNode(QGroupType.And);
				var colToCnd = new Dictionary<ColumnSchema, QGroupNode>();
				q.Condition = andCnd;
				foreach (var m in Matches) {
					switch (m) {
						case ColumnConditionMatch colCndMatch:
							var cnd = BuildCondition(colCndMatch);
							if (cnd != null) {
								if (!colToCnd.ContainsKey(colCndMatch.Column))
									colToCnd[colCndMatch.Column] = new QGroupNode(QGroupType.Or);
								colToCnd[colCndMatch.Column].Nodes.Add(cnd);
							}
							break;
						case GroupMatch grpMatch:
							andCnd.Nodes.Add(BuildGroup(grpMatch));
							break;
					}
				}
				foreach (var entry in colToCnd) {
					if (entry.Value.Nodes.Count==1) {
						andCnd.Nodes.Add(entry.Value.Nodes[0]);
					} else {
						andCnd.Nodes.Add(entry.Value);
					}
				}
				return q;
			}

			QNode BuildGroup(GroupMatch grpMatch) {
				var qGrp = new QGroupNode(grpMatch.Group == GroupMatcher.GroupType.And ? QGroupType.And : QGroupType.Or);
				foreach (var m in grpMatch.Matches) {
					switch (m) {
						case GroupMatch subGrpM:
							qGrp.Nodes.Add(BuildGroup(subGrpM));
							break;
						case ColumnConditionMatch cndM:
							qGrp.Nodes.Add(BuildCondition(cndM));
							break;
					}
				}
				return qGrp;
			}

			QNode BuildCondition(ColumnConditionMatch colCndMatch) {
				var cnd = resolveCondition(colCndMatch.Condition);
				switch (colCndMatch.Column.DataType) {
					case TableColumnDataType.String:
						var val = String.Concat(SearchQuery.Between(colCndMatch.Value.Start, colCndMatch.End, true));
						if (cnd == Conditions.Like) {
							val = val + "%";
							if (colCndMatch.Condition == ColumnConditionMatch.ConditionType.Contains)
								val = "%" + val;
						}
						return new QConditionNode(new QField(colCndMatch.Column.Name), cnd, new QConst(val));
					case TableColumnDataType.Number:
						if (colCndMatch.Value is NumberMatch numMatch)
							return new QConditionNode(new QField(colCndMatch.Column.Name), cnd, new QConst(numMatch.Value));
						break;
					case TableColumnDataType.Date:
						var dm = colCndMatch.Value as DateMatch;
						if (dm == null && colCndMatch.Value is DateOffsetMatch dtOffsetMatch) {
							dm = dtOffsetMatch.ToDate(DateTime.Now);
						}
						if (dm != null) {
							var year = dm.Year.HasValue ? dm.Year.Value : DateTime.Now.Year;
							var monthStart = dm.Month.HasValue ? dm.Month.Value : 1;
							var monthEnd = dm.Month.HasValue ? dm.Month.Value : 12;
							var dayStart = dm.Day.HasValue ? dm.Day.Value : 1;
							var dayEnd = dm.Day.HasValue ? dm.Day.Value : System.Globalization.CultureInfo.CurrentCulture.Calendar.GetDaysInMonth(year, monthEnd);

							var startDt = new DateTime(year, monthStart, dayStart);
							var endDt = new DateTime(year, monthEnd, dayEnd).AddDays(1).AddSeconds(-1);

							if (cnd == Conditions.Equal) {
								return QGroupNode.And(
									(QField)colCndMatch.Column.Name >= (QConst)startDt,
									(QField)colCndMatch.Column.Name <= (QConst)endDt);
							} else if (cnd == (Conditions.Equal | Conditions.Not)) {
								return QGroupNode.Or(
									(QField)colCndMatch.Column.Name < (QConst)startDt,
									(QField)colCndMatch.Column.Name > (QConst)endDt);
							} else {
								var dt = (cnd & Conditions.GreaterThan) == Conditions.GreaterThan ? endDt : startDt;
								return new QConditionNode((QField)colCndMatch.Column.Name, cnd, new QConst(dt));
							}
						}
						break;
				}
				return null;

				Conditions resolveCondition(ColumnConditionMatch.ConditionType cndType) {
					switch (cndType) {
						case ColumnConditionMatch.ConditionType.Exact:
						case ColumnConditionMatch.ConditionType.Equal:
							return Conditions.Equal;
						case ColumnConditionMatch.ConditionType.StartsWith:
						case ColumnConditionMatch.ConditionType.Contains:
							return Conditions.Like;
						case ColumnConditionMatch.ConditionType.GreaterThan:
							return Conditions.GreaterThan;
						case ColumnConditionMatch.ConditionType.GreaterThanOrEqual:
							return Conditions.GreaterThan | Conditions.Equal;
						case ColumnConditionMatch.ConditionType.LessThan:
							return Conditions.LessThan;
						case ColumnConditionMatch.ConditionType.LessThanOrEqual:
							return Conditions.LessThan | Conditions.Equal;
						case ColumnConditionMatch.ConditionType.NotEqual:
							return Conditions.Equal | Conditions.Not;
						default:
							throw new NotSupportedException($"Unknown condition type: {cndType}");
					}
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

		// for better matching words may be stemmed
		// in this example very simplified stemmer is used that handles '-s', '-ed', '-ing'
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

		/// <summary>
		/// Implements very simple stemming for English words.
		/// </summary>
		public class EnglishStemmer {

			public EnglishStemmer() {

			}

			bool IsPluralSuffix(char c) {
				return (c == 'p' || c == 'b' || c == 'g' || c == 'k' || c == 't' || c == 'd' || c == 'r' || c == 'n' || c == 'l' || c == 'v');
			}
			public string Stem(string word) {
				// this is hardcoded heuristics for english words
				// TODO: use wordnet to get correct forms
				if (word.Length > 5 && word.EndsWith("ses"))
					return word.Substring(0, word.Length - 2); // remove -es
				if (word.Length > 3 && word[word.Length - 1] == 's' && IsPluralSuffix(word[word.Length - 2]))
					return word.Substring(0, word.Length - 1); // remove -s
				if (word.Length > 5 && word.EndsWith("ed")) {
					return word.Substring(0, word.Length - 2);
				}
				if (word.Length > 4 && word.EndsWith("ing")) {
					return word.Substring(0, word.Length - 3);
				}
				return word;
			}

		}


	}
}