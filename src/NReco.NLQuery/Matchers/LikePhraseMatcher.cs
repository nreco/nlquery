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
using System.Globalization;

namespace NReco.NLQuery.Matchers {

	/// <summary>
	/// Matches alike phrase (some words in any order).
	/// </summary>
	public class LikePhraseMatcher : IMatcher {

		string[] Words;
		protected int TotalPhraseLength;

		protected Func<Match> GetMatch;

		public bool FirstPassOnly => true;
		public bool Recursive => false;

		public bool ScoreWeightByTotalLength { get; set; } = false;

		public LikePhraseMatcher(string[] matchWords, Func<Match> getMatch) {
			GetMatch = getMatch;
			Words = matchWords;
			TotalPhraseLength = 0;
			for (int i = 0; i < matchWords.Length; i++)
				TotalPhraseLength += matchWords[i].Length;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			var tokens = matchBag.Statement.Tokens;
			Token start = null;
			Token end = null;
			float score = 0f;
			float likeScore = 0f;
			int wordIdx = -1;
			var matched = new HashSet<int>();
			foreach (var t in tokens) {
				switch (t.Type) {
					case TokenType.Separator:
						continue;
					case TokenType.Word:
						if (Like(t.Value, ref likeScore, ref wordIdx)) {
							if (matched.Contains(wordIdx)) {
								// phrase word double match
								yield return currentMatch();
							}
							matched.Add(wordIdx);
							if (start == null) {
								start = t;
							}
							end = t;
							score += likeScore;
						} else {
							if (start != null) {
								yield return currentMatch();
							}
						}
						break;
					default:
						if (start != null) {
							yield return currentMatch();
						}
						break;
				}

			}

			void reset() {
				start = end = null;
				score = 0f;
				matched.Clear();
			}
			Match currentMatch() {
				var m = GetMatch();
				m.Start = start;
				m.End = end;
				m.Score = score;
				reset();
				return m;
			}
		}

		IEnumerable<Match> MatchSingleWord(IEnumerable<Token> tokens) {
			// simplified routine
			var word = Words[0];
			foreach (var t in tokens)
				if (t.Type == TokenType.Word) {
					var idx = word.IndexOf(t.Value, StringComparison.OrdinalIgnoreCase);
					if (idx>=0) { 
						var m = GetMatch();
						m.Score = ((float)t.Value.Length)/word.Length;
						if (idx > 0)
							m.Score /= 2; // not word start penalty
						m.Start = t;
						m.End = t;
						yield return m;
					}
				}
			yield break;
		}

		float GetScore(int wordIdx, string matchStr, int matchIdx) {
			var score = 0f;
			if (ScoreWeightByTotalLength) {
				score = ((float)matchStr.Length) / TotalPhraseLength; // weighted by number of chars
			} else {
				var wordScore = ((float)matchStr.Length) / Words[wordIdx].Length;
				score = wordScore / Words.Length;  // weighted by number of words
			}
			if (matchIdx > 0) {
				// starts-with boost
				// (maxScore - score) / 2;
				score /= 2; // not word start penalty
			}
			return score;
		}

		bool Like(string s, ref float score, ref int wordIdx) {
			for (int i=0; i<Words.Length; i++) {
				var word = Words[i];
				var idx = word.IndexOf(s, StringComparison.OrdinalIgnoreCase);
				if (idx >= 0) {
					score = GetScore(i, s, idx);
					wordIdx = i;
					return true;
				}
			}
			return false;
		}

	}
}
