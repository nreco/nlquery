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
using System.Text;

namespace NReco.NLQuery.Matchers {

	/// <summary>
	/// Detects comparison operators or phrase equivalents between two matches and merges them into one.
	/// </summary>
	public class ComparisonMatcher : IMatcher {

		public bool FirstPassOnly => false;
		public bool Recursive => false;

		Func<Match, bool> LeftPartPredicate;
		Func<Match, ComparisonType, Match, Match> GetComparisonMatch;

		public IEnumerable<KeyValuePair<string[], ComparisonType>> PhraseComparisonTypes { get; set; }

		public Func<string,bool> IsPhraseStopWord { get; set; }

		public ComparisonMatcher(Func<Match,bool> leftPartPredicate, Func<Match,ComparisonType,Match, Match> getComparisonMatch) {
			LeftPartPredicate = leftPartPredicate;
			GetComparisonMatch = getComparisonMatch;
		}

		private bool MatchPhraseOp(Token[] tokens, ref int idx, out ComparisonType cmp, out int cmpTokensCount, bool goReverse = false) {
			cmp = 0;
			cmpTokensCount = 0;
			foreach (var entry in PhraseComparisonTypes) {
				if (entry.Key.Length>0) {
					int startIdx = idx;
					if (goReverse ? matchInReverse(entry, ref startIdx) : match(entry, ref startIdx)) {
						cmp = entry.Value;
						cmpTokensCount = entry.Key.Length;
						idx = startIdx;
						return true;
					}
				}
			}
			return false;

			bool match(KeyValuePair<string[], ComparisonType> entry, ref int startIdx) {
				for (int i = 0; i < entry.Key.Length; i++) {
					// skip separators
					while (startIdx < tokens.Length && tokens[startIdx].Type==TokenType.Separator)
						startIdx++;
					if (tokens.Length<=startIdx || !String.Equals(entry.Key[i], tokens[startIdx].Value, StringComparison.OrdinalIgnoreCase)) { 
						if (IsPhraseStopWord!=null && startIdx<tokens.Length && IsPhraseStopWord(tokens[startIdx].Value)) {
							startIdx++; // skip stop word
							i--; // check the same entry again
							continue;
						}
						return false;
					}
					startIdx++;
				}
				return true;
			}

			bool matchInReverse(KeyValuePair<string[], ComparisonType> entry, ref int startIdx) {
				for (int i = entry.Key.Length-1; i>=0; i--) {
					// skip separators
					while (startIdx >= 0 && tokens[startIdx].Type==TokenType.Separator)
						startIdx--;
					if (startIdx<=0 || !String.Equals(entry.Key[i], tokens[startIdx].Value, StringComparison.OrdinalIgnoreCase)) {
						if (IsPhraseStopWord!=null && startIdx>=0 && IsPhraseStopWord(tokens[startIdx].Value)) {
							startIdx--; // skip stop word
							i++; // check the same entry again
							continue;
						}
						return false;
					}
					startIdx--;
				}
				return true;
			}


		}

		private bool MatchMathOp(Token[] tokens, ref int idx, out ComparisonType cmp) {
			var nextToken = (idx + 1) < tokens.Length ? tokens[idx + 1] : null;
			switch (tokens[idx].Value) {
				case "=":
					if (nextToken?.Value=="=") {
						idx++;
					}
					cmp = ComparisonType.Equal;
					return true;
				case ">":
					cmp = ComparisonType.GreaterThan;
					if (nextToken?.Value=="=") {
						idx++;
						cmp = ComparisonType.GreaterThanOrEqual;
					}
					return true;
				case "<":
					cmp = ComparisonType.LessThan;
					if (nextToken?.Value == "=") {
						idx++;
						cmp = ComparisonType.LessThanOrEqual;
					}
					return true;
			}
			cmp = 0;
			return false;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			foreach (var cmpLeftPart in matchBag.Matches) {
				if (LeftPartPredicate(cmpLeftPart)) {
					var endIdx = matchBag.Statement.GetIndex(cmpLeftPart.End);
					var tokens = matchBag.Statement.Tokens;
					ComparisonType cmp = 0;
					int cmpTokens = 0;
					// direct order: <entity> <op> <value> ("order year greater 2023")
					for (int i = endIdx + 1; i < tokens.Length-1 /* op cannot be last token */ ; i++) {
						var t = tokens[i];
						switch (t.Type) {
							case TokenType.Separator:
								// skip separators
								continue;
							case TokenType.Math:
								// hint force
								if (MatchMathOp(tokens, ref i, out cmp)) {
									continue;  // follow to read right part
								}
								break;
							case TokenType.Number:
							case TokenType.Word:
								if (cmp > 0) {
									bool hasMatches = false;
									foreach (var m in matchBag.FindByStart(t)) {
										hasMatches = true;
										var mergedMatch = GetComparisonMatch(cmpLeftPart, cmp, m);
										if (mergedMatch != null) {
											mergedMatch.MatchedTokensCount = cmpLeftPart.MatchedTokensCount+cmpTokens+m.MatchedTokensCount;
											mergedMatch.Start = cmpLeftPart.Start;
											mergedMatch.End = m.End;
											if (mergedMatch.Score == 0f) {
												mergedMatch.Score = (cmpLeftPart.Score + m.Score) / 2;
											}
											yield return mergedMatch;
										}
									}
									if (!hasMatches) {
										// no matches, lets try to create comparison match by next token
										var mergedMatch = GetComparisonMatch(cmpLeftPart, cmp, new StubMatch { Start = t, End = t });
										if (mergedMatch != null) {
											mergedMatch.MatchedTokensCount = cmpLeftPart.MatchedTokensCount+cmpTokens+mergedMatch.MatchedTokensCount;
											mergedMatch.Start = cmpLeftPart.Start;
											mergedMatch.End = t;
											if (mergedMatch.Score == 0f)
												mergedMatch.Score = (cmpLeftPart.Score + Match.ScoreMaybe) / 2;
											yield return mergedMatch;
										}
									}
								} else {
									if (PhraseComparisonTypes != null)
										if (MatchPhraseOp(tokens, ref i, out cmp, out cmpTokens))
											continue; // next: read right part
								}
								break;
						}
						break; // stop
					}

					// reverse order: <op> <value> <entity> ("more than 5 items")
					var leftPartPrevToken = matchBag.Statement.Prev(cmpLeftPart.Start, (t) => t.Type!=TokenType.Separator);
					if (leftPartPrevToken!=null)
						foreach (var cmpRightPart in matchBag.FindByEnd(leftPartPrevToken)) {
							var rightPartPrevToken = matchBag.Statement.Prev(cmpRightPart.Start,
									(t) => t.Type!=TokenType.Separator);
							if (rightPartPrevToken==null)
								continue;
							var i = matchBag.Statement.GetIndex(rightPartPrevToken);
							cmp = 0;
							cmpTokens = 0;
							switch (rightPartPrevToken.Type) {
								case TokenType.Math:
									while (i>0 && tokens[i-1].Type==TokenType.Math)
										i--;
									MatchMathOp(tokens, ref i, out cmp);
									break;
								case TokenType.Number:
								case TokenType.Word:
									if (PhraseComparisonTypes != null)
										MatchPhraseOp(tokens, ref i, out cmp, out cmpTokens, goReverse:true);
									break;
							}
							if (cmp>0) {
								var mergedMatch = GetComparisonMatch(cmpLeftPart, cmp, cmpRightPart);
								if (mergedMatch != null) {
									mergedMatch.MatchedTokensCount = cmpLeftPart.MatchedTokensCount+cmpTokens+cmpRightPart.MatchedTokensCount;
									mergedMatch.Start = tokens[i];
									mergedMatch.End = cmpLeftPart.End;
									if (mergedMatch.Score == 0f) {
										mergedMatch.Score = (cmpLeftPart.Score + cmpRightPart.Score) / 2;
									}
									yield return mergedMatch;
								}
							}
						}

				}
			}
		}

		public enum ComparisonType {
			Equal = 1,
			LessThan = 2,
			GreaterThan = 4,
			LessThanOrEqual = 1 + 2,
			GreaterThanOrEqual = 4 + 1,
			NotEqual = 8,
			Like = 16
		}

	}

}