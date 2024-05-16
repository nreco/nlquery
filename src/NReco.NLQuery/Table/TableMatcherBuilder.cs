/*
 *  Copyright 2016-2019 Vitaliy Fedorchenko (nrecosite.com)
 *
 *  Licensed under NLQuery Source Code License (see LICENSE file).
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS 
 *  OF ANY KIND, either express or implied.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Globalization;


using NReco.NLQuery.Matchers;

namespace NReco.NLQuery.Table {

	/// <summary>
	/// Constructs <see cref="IMatcher"/>s by <see cref="TableSchema"/>.
	/// </summary>
	public class TableMatcherBuilder {

		Tokenizer Tokenizer;
		List<IMatcher> Matchers;
		List<IMatcher> KeywordMatchers;
		bool MatchNumbers = false;
		bool MatchDates = false;
		protected Options Opts;

		public TableMatcherBuilder(Options options = null) {
			Opts = options ?? new Options();
			Tokenizer = new Tokenizer();
			Matchers = new List<IMatcher>();
			KeywordMatchers = new List<IMatcher>();
		}

		public IMatcher[] Build() {
			var resMatchers = new List<IMatcher>();
			var hintMatcher = ConfigureHintMatcher();
			if (Opts.StopWords != null && Opts.StopWords.Length > 0) {
				// wrap with stop-words filter
				var stopWords = new StopWordsFilter(Opts.StopWords);
				resMatchers.Add(new TokenFilterMatcher(
					new CompositeMatcher(KeywordMatchers.ToArray()),
					token => token.Type != TokenType.Word || !stopWords.IsStopWord(token.ValueLowerCase)
				));
				// process hints in respect to stop words
				hintMatcher = new TokenFilterMatcher(hintMatcher,
					token => token.Type != TokenType.Word || !stopWords.IsStopWord(token.ValueLowerCase)
				);
			} else {
				resMatchers.AddRange(KeywordMatchers);
			}

			if (KeywordMatchers.Count > 0) {
				// TBD: add merge phrase matched for column matching!!
				resMatchers.Add(new MergePhraseMatcher<ColumnMatch>(
					(st, m1, m2) => {
						if (m1.Column!=m2.Column || !MatchedCaptionsCanBeMerged(m1.MatchedCaption,m2.MatchedCaption)
							|| m1.Score==Match.ScoreCertain || m2.Score==Match.ScoreCertain)
							return null;
						var mergedScore = IfBoostScore(m1)+IfBoostScore(m2);
						//if (mergedScore>Match.ScoreCertain)
						//	return null; // doesn't seem like 2 separated parts of the same name
						var betweenWordOrNumCnt = st.Between(m1.End, m2.Start, false).Where(t => t.Type==TokenType.Number||t.Type==TokenType.Number).Count();
						if (betweenWordOrNumCnt>3)
							return null; // too far
						if (betweenWordOrNumCnt>0)
							mergedScore -= mergedScore*(0.25f*betweenWordOrNumCnt);
						return new ColumnMatch(m1.Column) { 
							Start = m1.Start,
							End = m2.End,
							Score = mergedScore,
							MatchedCaption = m1.MatchedCaption ?? m2.MatchedCaption
						};
					}
				));
				resMatchers.Add(new MergePhraseMatcher<ColumnConditionMatch>(
					(st, m1, m2) => {
						if (m1.Column != m2.Column || m1.MatchedValue == null || m1.MatchedValue != m2.MatchedValue
							|| m1.Score==Match.ScoreCertain || m2.Score==Match.ScoreCertain)
							return null;
						var betweenVal = String.Concat(st.Between(m1.Start, m2.End).Select(t => t.Value));
						var idx = m1.MatchedValue.IndexOf(betweenVal, StringComparison.OrdinalIgnoreCase);
						if (idx < 0) {
							// both match the same value, so lets return a new exact match
							// with score that takes into account relation between 2 merged matches
							// and non-matched tokens between them
							var mergedScore = m1.Score+m2.Score;
							var betweenWordOrNumCnt = st.Between(m1.End, m2.Start, false).Where(t=>t.Type==TokenType.Number||t.Type==TokenType.Number).Count();
							if (betweenWordOrNumCnt>3)
								return null; // too far
							if (betweenWordOrNumCnt>0)
								mergedScore -= mergedScore*(0.25f*betweenWordOrNumCnt);
							//Console.WriteLine($"MERGED: m1=[{String.Concat(st.Between(m1.Value.Start, m1.Value.End))}] m2=[{String.Concat(st.Between(m2.Value.Start, m2.Value.End))}] MatchValue=[{m1.MatchedValue}] score={mergedScore}");
							return new ColumnConditionMatch() {
								Column = m1.Column,
								Hint = m1.Hint,
								Start = m1.Start,
								End = m2.End,
								Condition = m1.Condition,
								MatchedValue = m1.MatchedValue,
								Value = m1,
								Score = mergedScore
							};
						}
						var contains = ColumnConditionMatch.ConditionType.Contains;
						if (idx == 0) {
							contains = ColumnConditionMatch.ConditionType.StartsWith;
							if (m1.MatchedValue.Length == betweenVal.Length)
								contains = ColumnConditionMatch.ConditionType.Exact;
						}
						return new ColumnConditionMatch() {
							Column = m1.Column,
							Hint = m1.Hint,
							Start = m1.Start,
							End = m2.End,
							Condition = contains,
							MatchedValue = m1.MatchedValue,
							// phrase is weighed by length
							Score = ((float)betweenVal.Length) / m1.MatchedValue.Length
						};
					}
				));
				resMatchers.Add(hintMatcher);
			}

			if (MatchNumbers && !Matchers.Any(m => m is NumberMatcher))
				resMatchers.Add(new NumberMatcher());
			if (MatchDates) {
				ConfigureDateMatchers(resMatchers);
			}
			ConfigureOperatorMatchers(resMatchers);

			resMatchers.AddRange(Matchers);

			return resMatchers.ToArray();
		}

		float IfBoostScore(ColumnMatch colMatch) => colMatch.MatchedCaption==null ? 0.5f : colMatch.Score;

		bool MatchedCaptionsCanBeMerged(string c1, string c2) {
			if (c1==null && c2==null)
				return false;
			if (c1!=null && c2!=null)
				return c1==c2;
			return true;
		}

		bool EnsureColumnDataType(ColumnSchema column, TableColumnDataType dataType) {
			return column.DataType == dataType || column.DataType == TableColumnDataType.Unknown;
		}

		protected virtual void ConfigureOperatorMatchers(List<IMatcher> resMatchers) {
			if (Opts.MatchMathOperators) {
				var cmpMatcher = new ComparisonMatcher(
					(m) => m is ColumnMatch,
					(left, cmp, right) => {
						var leftColM = (ColumnMatch)left;
						Match rightM = null;
						float score = 0;
						if (right is NumberMatch && EnsureColumnDataType(leftColM.Column,TableColumnDataType.Number)) {
							score = leftColM.Score;
							rightM = right;
						} else if (right is DateMatch && EnsureColumnDataType(leftColM.Column,TableColumnDataType.Date)) {
							score = leftColM.Score;
							rightM = right;
						} else if (right is ColumnConditionMatch rColCndM
								&& (rColCndM.Column == leftColM.Column || rColCndM.Hint == null)
								&& EnsureColumnDataType(leftColM.Column,TableColumnDataType.String)) {
							rightM = new StubMatch() {
								Start = rColCndM.Start,
								End = rColCndM.Column == leftColM.Column ? rColCndM.End : rColCndM.Start
							};
						} else if (right is StubMatch) {
							rightM = right;
						}
						if (rightM != null)
							return new ColumnConditionMatch(leftColM.Column, getColumnCondition(cmp), rightM) {
								Hint = left,
								Score = score > 0 ? score: Match.ScoreMaybe + (leftColM.Score + rightM.Score) / 4
							};
						return null;
					}
				);
				if (Opts.MathOperatorPhrases != null) {
					var cmpMatcherPhrases = new List<KeyValuePair<string[], ComparisonMatcher.ComparisonType>>();
					foreach (var entry in Opts.MathOperatorPhrases) {
						var wordTokens = Tokenizer.Parse(entry.Key).Where(t => t.Type == TokenType.Word).ToArray();
						cmpMatcherPhrases.Add(new KeyValuePair<string[], ComparisonMatcher.ComparisonType>(
								wordTokens.Select(t => t.Value).ToArray(), entry.Value
							));
					}
					cmpMatcher.PhraseComparisonTypes = cmpMatcherPhrases;
					if (Opts.StopWords != null && Opts.StopWords.Length > 0) {
						// register stop-words for op-phrases
						var stopWords = new StopWordsFilter(Opts.StopWords);
						cmpMatcher.IsPhraseStopWord = stopWords.IsStopWord;
					}
					resMatchers.Add(cmpMatcher);
				}
			}
			if (Opts.MatchBoolOperators) {
				var grpMatcher = new GroupMatcher(
					(m, matchBag) => (m is ColumnConditionMatch || m is GroupMatch),
					(left, grp, right, matchBag) => {
						if ((right is ColumnConditionMatch || right is GroupMatch) && !GroupMatch.IsAlreadyInGroup(matchBag, left, right)) {
							var grpMatch = new GroupMatch(grp);
							grpMatch.Matches.Add(left);
							grpMatch.Matches.Add(right);
							return grpMatch;
						}
						return null;
					}
				);
				var groupPhrases = new List<KeyValuePair<string[], GroupMatcher.GroupType>>();
				if (Opts.GroupAndPhrases != null)
					foreach (var andKeyword in Opts.GroupAndPhrases)
						groupPhrases.Add(new KeyValuePair<string[], GroupMatcher.GroupType>(new[] { andKeyword }, GroupMatcher.GroupType.And));
				if (Opts.GroupOrPhrases != null)
					foreach (var orKeyword in Opts.GroupOrPhrases)
						groupPhrases.Add(new KeyValuePair<string[], GroupMatcher.GroupType>(new[] { orKeyword }, GroupMatcher.GroupType.Or));
				grpMatcher.PhraseGroupTypes = groupPhrases;
				resMatchers.Add(grpMatcher);
			}

			ColumnConditionMatch.ConditionType getColumnCondition(ComparisonMatcher.ComparisonType cmp) {
				return (ColumnConditionMatch.ConditionType) ((int)cmp << 5);
			}
		}

		/// <summary>
		/// Configure matchers for tabular dataset described by <see cref="TableSchema"/>.
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		public TableMatcherBuilder Add(TableSchema table) {
			ConfigureMatchers(table);
			return this;
		}

		/// <summary>
		/// Add custom <see cref="IMatcher"/> to this builder.
		/// </summary>
		/// <param name="matcher"></param>
		/// <param name="keywordMatcher">if true this matcher will be wrapped with filter that removes stop-words.</param>
		/// <returns></returns>
		public TableMatcherBuilder Add(IMatcher matcher, bool keywordMatcher = false) {
			if (keywordMatcher) {
				KeywordMatchers.Add(matcher);
			} else {
				Matchers.Add(matcher);
			}
			return this;
		}

		protected IMatcher ConfigureHintMatcher() {
			return new HintMatcher<ColumnMatch>((hintMatch, valueMatch, force) => {
				switch (valueMatch) {
					case ColumnConditionMatch cndMatch:
						var sameColumn = hintMatch.Column == cndMatch.Column;
						var notContainsOrNotLowScore = cndMatch.Condition!=ColumnConditionMatch.ConditionType.Contains || cndMatch.Score>=(Match.ScoreMaybe/2);
						if ((sameColumn || force) && notContainsOrNotLowScore) {
							var m = new ColumnConditionMatch() {
								Column = hintMatch.Column,
								Hint = hintMatch,
								Condition = sameColumn ? cndMatch.Condition : ColumnConditionMatch.ConditionType.Contains,
								Value = cndMatch.Hint != null ? cndMatch.Value : valueMatch,
								MatchedValue = cndMatch.MatchedValue
							};
							if (sameColumn && valueMatch.Score>Match.ScoreMaybe) {
								float boost = 1f;
								if (force) {
									boost = 1f + hintMatch.Score;
								} else if (hintMatch.Score >= Match.ScoreMaybe) {
									boost = 1f + (hintMatch.Score - Match.ScoreMaybe);
								}
								m.Score = ((hintMatch.Score + valueMatch.Score) / 2) * boost;
							}
							return m;
						}
						break;
					case DateMatch dtMatch:
					case DateOffsetMatch dOfsMatch:
						if (EnsureColumnDataType(hintMatch.Column, TableColumnDataType.Date)) {
							return new ColumnConditionMatch() {
								Column = hintMatch.Column,
								Hint = hintMatch,
								Condition = ColumnConditionMatch.ConditionType.Exact,
								Value = valueMatch
							};
						}
						break;
					case NumberMatch numMatch:
						if (EnsureColumnDataType(hintMatch.Column, TableColumnDataType.Number)) {
							return new ColumnConditionMatch() {
								Column = hintMatch.Column,
								Hint = hintMatch,
								Score = (hintMatch.Score+numMatch.Score)/2f*0.9f,
								Condition = ColumnConditionMatch.ConditionType.Exact,
								Value = valueMatch
							};
						}
						break;
					case StubMatch m:
						return new ColumnConditionMatch() {
							Column = hintMatch.Column,
							Hint = hintMatch,
							Condition = ColumnConditionMatch.ConditionType.Contains,
							Value = valueMatch
						};
				}
				return null;
			});
		}

		protected virtual void ConfigureMatchers(TableSchema table) {
			var stopWords = Opts.StopWords != null && Opts.StopWords.Length > 0 ? new StopWordsFilter(Opts.StopWords) : null;

			foreach (var tblCaption in table.GetCaptionsToMatch())
				addCaptionMatcher(tblCaption, table.ExactMatchOnly, (wordsCount) => new TableMatch(table) );

			var hasNumberCols = false;
			var hasDateCols = false;
			foreach (var col in table.Columns) {
				foreach (var colCaption in col.GetCaptionsToMatch()) {
					addCaptionMatcher(colCaption, col.ExactMatchOnly, (wordsCount) => {
						var m = new ColumnMatch(col) { MatchedCaption = colCaption };
						if (col.CaptionBoostPhrases!=null && col.CaptionBoostPhrases.Length>0)
							m.Score = ((float)wordsCount)/(wordsCount+1); // multiplier: reduced score for match without boost
						return m;
					} );
				}
				if (col.CaptionBoostPhrases!=null)
					foreach (var boostPhrase in col.CaptionBoostPhrases) {
						var tokens = Tokenizer.Parse(boostPhrase).Where(t => !String.IsNullOrEmpty(t.Value) && t.Type!=TokenType.Separator);
						var exactMatcher = new ExactPhraseMatcher(tokens.Select(t => t.Value).ToArray(),
							() => new ColumnMatch(col) { 
								Score = 0.001f  // multiplier for boost-only phrase match (it should be merged with column's caption match)
							});
						Matchers.Add(exactMatcher);
					}
				if (col.Values != null && col.Values.Length > 0)
					KeywordMatchers.Add(new ListContainsMatcher(col.Values, (containsType, matchedVal) => {
						return new ColumnConditionMatch() {
							Column = col,
							Condition = (ColumnConditionMatch.ConditionType)containsType,
							MatchedValue = matchedVal.Value
						};
					}) { ApplyStemmer = Opts.ApplyStemmer });

				if (EnsureColumnDataType(col,TableColumnDataType.Date))
					hasDateCols = true;
				if (EnsureColumnDataType(col,TableColumnDataType.Number))
					hasNumberCols = true;
			}

			if (hasDateCols) {
				MatchDates = true;
			}
			if (hasNumberCols) {
				MatchNumbers = true;
			}

			var firstDateCol = table.Columns.Where(c => c.DataType == TableColumnDataType.Date).FirstOrDefault();
			if (firstDateCol != null) {
				Matchers.Add(new AssignDefaultDateColumnMatcher(firstDateCol));
			}

			void addCaptionMatcher(string caption, bool exactOnly, Func<int,Match> getMatch) {
				var captionTokens = Tokenizer.Parse(caption).Where(t => !String.IsNullOrEmpty(t.Value)).ToArray();
				var captionTokensWithoutSeparator = captionTokens.Where(t => t.Type != TokenType.Separator).ToArray();
				var captionWordOrNumOnly = captionTokensWithoutSeparator.Where(t => t.Type == TokenType.Word || t.Type==TokenType.Number);
				if (stopWords!=null)
					captionWordOrNumOnly = stopWords.RemoveStopWords(captionWordOrNumOnly);
				var captionWordOrNumOnlyArr = captionWordOrNumOnly.ToArray();
				if (exactOnly || captionTokensWithoutSeparator.Length != captionWordOrNumOnlyArr.Length) {
					var matchWords = captionTokensWithoutSeparator.Select(t => t.Value).ToArray();
					var exactCaptionMatcher = new ExactPhraseMatcher(matchWords, ()=>getMatch(matchWords.Length) );
					if (captionTokensWithoutSeparator.Length == captionTokens.Length)
						exactCaptionMatcher.AllowSeparators = false; // do not allow spaces
					Matchers.Add(exactCaptionMatcher);
				}
				if (!exactOnly) {
					var matchWords = captionWordOrNumOnlyArr.Select(t => t.Value).ToArray();
					KeywordMatchers.Add(
						new LikePhraseMatcher(matchWords, ()=>getMatch(matchWords.Length) ) {
							ApplyStemmer = Opts.ApplyStemmer
						});
				}
			}

		}

		protected virtual void ConfigureDateMatchers(List<IMatcher> matchers) {
			var dateMatcher = new DateMatcher();
			if (Opts.DateTimeFormat != null)
				dateMatcher.DateFormat = Opts.DateTimeFormat;
			matchers.Add(dateMatcher);

			addExactPhraseMatcher(Opts.YesterdayPhrases, new DateOffsetMatch() { Year = 0, Month = 0, Day = -1 }.Clone);
			addExactPhraseMatcher(Opts.TomorrowPhrases, new DateOffsetMatch() { Year = 0, Month = 0, Day = 1 }.Clone);
			addExactPhraseMatcher(Opts.TodayPhrases, new DateOffsetMatch() { Year = 0, Month = 0, Day = 0 }.Clone);

			addExactPhraseMatcher(Opts.PrevMonthPhrases, new DateOffsetMatch() { Year = 0, Month = -1 }.Clone);
			addExactPhraseMatcher(Opts.ThisMonthPhrases, new DateOffsetMatch() { Year = 0, Month = 0 }.Clone);
			addExactPhraseMatcher(Opts.NextMonthPhrases, new DateOffsetMatch() { Year = 0, Month = 1 }.Clone);

			addExactPhraseMatcher(Opts.PrevYearPhrases, new DateOffsetMatch() { Year = -1 }.Clone);
			addExactPhraseMatcher(Opts.ThisYearPhrases, new DateOffsetMatch() { Year = 0 }.Clone);
			addExactPhraseMatcher(Opts.NextYearPhrases, new DateOffsetMatch() { Year = 1 }.Clone);

			void addExactPhraseMatcher(string[] phrases, Func<Match> getMatch) {
				if (phrases == null)
					return;
				foreach (var phrase in phrases) {
					var wordTokens = Tokenizer.Parse(phrase).Where(t => t.Type == TokenType.Word).ToArray();
					matchers.Add(
						new ExactPhraseMatcher(wordTokens.Select(t => t.Value).ToArray(), getMatch)
					);
				}
			}
		}

		internal class AssignDefaultDateColumnMatcher : IMatcher {
			ColumnSchema DateColumn;

			public bool FirstPassOnly => false;
			public bool Recursive => false;

			internal AssignDefaultDateColumnMatcher(ColumnSchema dateColumn) {
				DateColumn = dateColumn;
			}

			public IEnumerable<Match> GetMatches(MatchBag matchBag) {
				foreach (var m in matchBag.Matches) {
					if (m is DateMatch || m is DateOffsetMatch) {
						if (matchBag.Matches.Any(mm => mm is ColumnConditionMatch cndM && cndM.Value==m))
							continue; // match is already a part of condition
						yield return new ColumnConditionMatch() {
							Column = DateColumn,
							Condition = ColumnConditionMatch.ConditionType.Exact,
							Value = m,
							Start = m.Start,
							End = m.End,
							Score = Match.ScoreMaybe
						};
					}
				}
			}
		}

		public class Options {
			public string[] YesterdayPhrases { get; set; } = new[] { "yesterday" };
			public string[] TomorrowPhrases { get; set; } = new[] { "tomorrow" };
			public string[] TodayPhrases { get; set; } = new[] { "today" };
			public string[] ThisMonthPhrases { get; set; } = new[] { "this month", "current month" };
			public string[] PrevMonthPhrases { get; set; } = new[] { "prev month", "previous month", "last month" };
			public string[] NextMonthPhrases { get; set; } = new[] { "next month" };
			public string[] ThisYearPhrases { get; set; } = new[] { "this year", "current year" };
			public string[] PrevYearPhrases { get; set; } = new[] { "prev year", "previous year", "last year" };
			public string[] NextYearPhrases { get; set; } = new[] { "next year" };

			public string[] GroupAndPhrases { get; set; } = new[] { "and" };
			public string[] GroupOrPhrases { get; set; } = new[] { "or" };

			public Dictionary<string, ComparisonMatcher.ComparisonType> MathOperatorPhrases = new Dictionary<string, ComparisonMatcher.ComparisonType>() {
				{"equal", ComparisonMatcher.ComparisonType.Equal},
				{"equals", ComparisonMatcher.ComparisonType.Equal},
				{"not equals", ComparisonMatcher.ComparisonType.NotEqual},
				{"not equal", ComparisonMatcher.ComparisonType.NotEqual},
				{"before", ComparisonMatcher.ComparisonType.LessThan},
				{"below", ComparisonMatcher.ComparisonType.LessThan},
				{"less than", ComparisonMatcher.ComparisonType.LessThan},
				{"less", ComparisonMatcher.ComparisonType.LessThan},
				{"smaller than", ComparisonMatcher.ComparisonType.LessThan},
				{"fewer", ComparisonMatcher.ComparisonType.LessThan},
				{"under", ComparisonMatcher.ComparisonType.LessThan},
				{"ending with", ComparisonMatcher.ComparisonType.LessThanOrEqual},
				{"after", ComparisonMatcher.ComparisonType.GreaterThan},
				{"above", ComparisonMatcher.ComparisonType.GreaterThan},
				{"greater than", ComparisonMatcher.ComparisonType.GreaterThan},
				{"greater", ComparisonMatcher.ComparisonType.GreaterThan},
				{"more than", ComparisonMatcher.ComparisonType.GreaterThan},
				{"more", ComparisonMatcher.ComparisonType.GreaterThan},
				{"larger", ComparisonMatcher.ComparisonType.GreaterThan},
				{"over", ComparisonMatcher.ComparisonType.GreaterThan},
				{"starting with", ComparisonMatcher.ComparisonType.GreaterThanOrEqual},
			};

			public string[] StopWords { get; set; }

			public DateTimeFormatInfo DateTimeFormat { get; set; }

			public bool MatchMathOperators { get; set; } = true;
			public bool MatchBoolOperators { get; set; } = true;

			public Func<string, string> ApplyStemmer { get; set; }
		}

	}
}
