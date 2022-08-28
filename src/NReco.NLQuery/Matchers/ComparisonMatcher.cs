﻿/*
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

		public ComparisonMatcher(Func<Match,bool> leftPartPredicate, Func<Match,ComparisonType,Match, Match> getComparisonMatch) {
			LeftPartPredicate = leftPartPredicate;
			GetComparisonMatch = getComparisonMatch;
		}

		private bool MatchPhraseOp(Token[] tokens, ref int idx, out ComparisonType cmp) {
			cmp = 0;
			foreach (var entry in PhraseComparisonTypes) {
				if (entry.Key.Length>0) {
					int startIdx = idx;
					if (match(entry, ref startIdx)) {
						cmp = entry.Value;
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
					if (tokens.Length<=startIdx || !String.Equals(entry.Key[i], tokens[startIdx].Value, StringComparison.OrdinalIgnoreCase))
						return false;
					startIdx++;
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
			foreach (var cmpLeftPart in matchBag.Matches)
				if (LeftPartPredicate(cmpLeftPart)) {
					var endIdx = matchBag.Statement.GetIndex(cmpLeftPart.End);
					var tokens = matchBag.Statement.Tokens;
					ComparisonType cmp = 0;
					for (int i = endIdx + 1; i < tokens.Length-1 /* this should not be last token */ ; i++) {
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
											mergedMatch.Start = cmpLeftPart.Start;
											mergedMatch.End = t;
											if (mergedMatch.Score == 0f)
												mergedMatch.Score = (cmpLeftPart.Score + Match.ScoreMaybe) / 2;
											yield return mergedMatch;
										}
									}
								} else {
									if (PhraseComparisonTypes != null)
										if (MatchPhraseOp(tokens, ref i, out cmp))
											continue; // next: read right part
								}
								break;
						}
						break; // stop
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