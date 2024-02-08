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

namespace NReco.NLQuery.Matchers {

	/// <summary>
	/// Detects boolean operators AND/OR between 2 matches and merges them into one match.
	/// </summary>
	/// <remarks>This matcher recognizes <c>&amp;&amp;</c> <c>||</c>. 
	/// Word-based conjunctions should be configured with <see cref="PhraseGroupTypes"/> property, for example:
	/// </remarks>
	/// <example><code>
	/// grpMatcher.PhraseGroupTypes = new[] {
	///		new KeyValuePair&lt;string[],GroupMatcher.GroupType&gt;(new[]{"and"}, GroupMatcher.GroupType.And),
	///		new KeyValuePair&lt;string[], GroupMatcher.GroupType&gt;(new[]{"or"}, GroupMatcher.GroupType.Or)
	///	};
	/// </code></example>
	public class GroupMatcher : IMatcher {

		public bool FirstPassOnly => false;
		public bool Recursive => true;

		Func<Match, MatchBag, bool> LeftPartPredicate;
		Func<Match, GroupType, Match, MatchBag, Match> GetGroupMatch;

		public IEnumerable<KeyValuePair<string[], GroupType>> PhraseGroupTypes { get; set; }

		public GroupMatcher(Func<Match,MatchBag,bool> leftPartPredicate, Func<Match,GroupType,Match,MatchBag,Match> getGroupMatch) {
			LeftPartPredicate = leftPartPredicate;
			GetGroupMatch = getGroupMatch;
		}

		private bool MatchPhraseOp(Token[] tokens, ref int idx, out GroupType cmp, out int tokensCount) {
			cmp = 0;
			tokensCount = 0;
			foreach (var entry in PhraseGroupTypes) {
				if (entry.Key.Length>0) {
					int startIdx = idx;
					if (match(entry, ref startIdx)) {
						cmp = entry.Value;
						tokensCount = entry.Key.Length;
						idx = startIdx;
						return true;
					}
				}
			}
			return false;

			bool match(KeyValuePair<string[], GroupType> entry, ref int startIdx) {
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

		private bool MatchGroupOp(Token[] tokens, ref int idx, out GroupType cmp) {
			var nextToken = (idx + 1) < tokens.Length ? tokens[idx + 1] : null;
			switch (tokens[idx].Value) {
				case "|":
					if (nextToken?.Value=="|") {
						idx++;
						cmp = GroupType.Or;
						return true;
					}
					break;
				case "&":
					if (nextToken?.Value=="&") {
						idx++;
						cmp = GroupType.And;
					}
					break;
			}
			cmp = 0;
			return false;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			foreach (var cmpLeftPart in matchBag.Matches)
				if (LeftPartPredicate(cmpLeftPart, matchBag)) {
					var endIdx = matchBag.Statement.GetIndex(cmpLeftPart.End);
					var tokens = matchBag.Statement.Tokens;
					GroupType cmp = 0;
					int cmpTokensCount = 0;
					for (int i = endIdx + 1; i < tokens.Length-1 /* this should not be last token */ ; i++) {
						var t = tokens[i];
						switch (t.Type) {
							case TokenType.Separator:
								// skip separators
								continue;
							case TokenType.Math:
								// hint force
								if (MatchGroupOp(tokens, ref i, out cmp)) {
									continue;  // follow to read right part
								}
								break;
							case TokenType.Number:
							case TokenType.Word:
								if (cmp > 0) {
									foreach (var m in matchBag.FindByStart(t)) {
										var mergedMatch = GetGroupMatch(cmpLeftPart, cmp, m, matchBag);
										if (mergedMatch != null) {
											mergedMatch.MatchedTokensCount = cmpLeftPart.MatchedTokensCount + cmpTokensCount + m.MatchedTokensCount;
											mergedMatch.Start = cmpLeftPart.Start;
											mergedMatch.End = m.End;
											if (mergedMatch.Score == 0f) {
												mergedMatch.Score = (cmpLeftPart.Score + m.Score) / 2;
											}
											yield return mergedMatch;
											// this is recursive matcher which may combine matches that produced by itself
											// for correct merge only 1 match is returned per pass
											// (case like 'a=1 or a=2 or a=3')
											yield break;
										}
									}
								} else {
									if (PhraseGroupTypes != null)
										if (MatchPhraseOp(tokens, ref i, out cmp, out cmpTokensCount))
											continue; // next: read right part
								}
								break;
						}
						break; // stop
					}
				}

		}

		public enum GroupType {
			And = 1,
			Or = 2
		}

	}

}