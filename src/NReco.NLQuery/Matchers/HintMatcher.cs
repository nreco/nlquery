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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace NReco.NLQuery.Matchers {

	/// <summary>
	/// Implements 'hint'-style matching for pattern like '[column] [value]' or '[column]:[value]'.
	/// </summary>
	public class HintMatcher<T> : IMatcher where T : Match {

		public bool FirstPassOnly => false;
		public bool Recursive => false;

		Func<T,Match,bool, Match> GetMatch;

		public HintMatcher(Func<T,Match,bool,Match> getMatch) {
			GetMatch = getMatch;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			foreach (var hintCandidate in matchBag.Matches)
				if (hintCandidate is T hintM) {
					var endIdx = matchBag.Statement.GetIndex(hintM.End);
					var tokens = matchBag.Statement.Tokens;
					var hintForce = false;
					// <hint> <value> or <hint>: <value>
					var directOrderMatched = false;
					for (int i = endIdx+1; i<tokens.Length; i++) {
						var t = tokens[i];
						switch (t.Type) {
							case TokenType.Separator:
								// skip separators
								continue;
							case TokenType.Punctuation:
								// hint force
								if (t.Value==":" && !hintForce) {
									hintForce = true;
									continue;
								}
								break;
							case TokenType.Number:
							case TokenType.Word:
								bool hasMatches = false;
								foreach (var m in matchBag.FindByStart(t)) {
									hasMatches = true;
									var mergedMatch = GetMatch(hintM, m, hintForce);
									if (mergedMatch != null) {
										mergedMatch.MatchedTokensCount = hintM.MatchedTokensCount + m.MatchedTokensCount;
										mergedMatch.Start = hintM.Start;
										mergedMatch.End = m.End;
										if (mergedMatch.Score==0f) {
											mergedMatch.Score = (hintM.Score+m.Score)/2;
										}
										directOrderMatched = true;
										yield return mergedMatch;
									}
								}
								if (!hasMatches) {
									// no match
									var mergedMatch = GetMatch(hintM, new StubMatch { Start = t, End = t }, hintForce);
									if (mergedMatch!=null) {
										mergedMatch.MatchedTokensCount = hintM.MatchedTokensCount+1;
										mergedMatch.Start = hintM.Start;
										mergedMatch.End = t;
										if (mergedMatch.Score==0f)
											mergedMatch.Score = hintForce || hintM.Score<Match.ScoreMaybe ?
																	hintM.Score : (hintM.Score+Match.ScoreMaybe)/2;
										yield return mergedMatch;
									}
								}
								break;
						}
						break;
					}
					// <value> <hint>
					var prevNonSpaceToken = matchBag.Statement.Prev(hintM.Start, (t) => t.Type!=TokenType.Separator);
					if (prevNonSpaceToken!=null && (prevNonSpaceToken.Type==TokenType.Word || prevNonSpaceToken.Type==TokenType.Number)) {
						//bool hasMatches = false;
						foreach (var m in matchBag.FindByEnd(prevNonSpaceToken)) {
							//hasMatches = true;
							var mergedMatch = GetMatch(hintM, m, false);
							if (mergedMatch != null) {
								mergedMatch.MatchedTokensCount = hintM.MatchedTokensCount + m.MatchedTokensCount;
								mergedMatch.Start = m.Start;
								mergedMatch.End = hintM.End;
								if (mergedMatch.Score==0f) {
									mergedMatch.Score = (hintM.Score+m.Score)/2;
								}
								mergedMatch.Score *= directOrderMatched ? 0.5f :0.9f; // penalty for reverse order
								yield return mergedMatch;
							}
						}
						/*if (!hasMatches) {
							// no match
							var mergedMatch = GetMatch(hintM, new StubMatch { Start = prevNonSpaceToken, End = prevNonSpaceToken }, false);
							if (mergedMatch!=null) {
								mergedMatch.Start = prevNonSpaceToken;
								mergedMatch.End = hintM.End;
								if (mergedMatch.Score==0f)
									mergedMatch.Score = (hintM.Score+Match.ScoreMaybe)/2;
								yield return mergedMatch;
							}
						}*/
					}
				}
		}


	}
}
