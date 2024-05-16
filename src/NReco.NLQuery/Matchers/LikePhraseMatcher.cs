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
		protected int PhraseWordsCount;

		protected Func<Match> GetMatch;

		public bool FirstPassOnly => true;
		public bool Recursive => false;

		public bool ScoreWeightByTotalLength { get; set; } = false;

		public Func<string,string> ApplyStemmer { get; set; }

		public LikePhraseMatcher(string[] matchWords, Func<Match> getMatch) {
			GetMatch = getMatch;
			Words = matchWords;
			TotalPhraseLength = Words.Sum(w=>w.Length);
			PhraseWordsCount = Words.Length;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			var tokens = matchBag.Statement.Tokens;
			Token start = null;
			Token end = null;
			float score = 0f;
			float likeScore = 0f;
			string matchedWord = null;
			var matched = new HashSet<string>();
			foreach (var t in tokens) {
				switch (t.Type) {
					case TokenType.Separator:
						continue;
					case TokenType.Number:
					case TokenType.Word:
						if (Like(t.Value, ref likeScore, ref matchedWord)) {
							if (matched.Contains(matchedWord)) {
								// phrase word double match
								var m = currentMatch();
								if (m!=null)
									yield return m;
							}
							matched.Add(matchedWord);
							if (start == null) {
								start = t;
							}
							end = t;
							score += likeScore;
						} else {
							var m = currentMatch();
							if (m!=null)
								yield return m;
						}
						break;
					default: {
							var m = currentMatch();
							if (m!=null)
								yield return m;
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
				if (start==null)
					return null;
				var	m = GetMatch();
				m.MatchedTokensCount = matched.Count;
				m.Start = start;
				m.End = end;
				// if GetMatch returns non-zero score, use it as a multiplier
				m.Score = m.Score>0 ? m.Score*score : score;
				reset();
				return m;
			}
		}

		float GetScore(string word, string matchStr, int matchIdx) {
			var score = 0f;
			if (ScoreWeightByTotalLength) {
				score = ((float)matchStr.Length) / TotalPhraseLength; // weighted by number of chars
			} else {
				var wordScore = ((float)matchStr.Length) / word.Length;
				score = wordScore / PhraseWordsCount;  // weighted by number of words
			}
			if (matchIdx > 0) {
				// starts-with boost
				// (maxScore - score) / 2;
				score /= 2; // not word start penalty
			}
			return score;
		}

		bool Like(string s, ref float score, ref string matchedWord) {
			for (int i=0; i<Words.Length; i++) {
				var word = Words[i];
				var idx = word.IndexOf(s, StringComparison.OrdinalIgnoreCase);
				if (idx<0 && ApplyStemmer!=null) {
					var ss = ApplyStemmer(s);
					var stemmedWord = ApplyStemmer(word);
					if (ss!=s) {
						idx = stemmedWord.IndexOf(ss, StringComparison.OrdinalIgnoreCase);
						if (idx>=0)
							s = ss;
					}
				}
				if (idx >= 0) {
					score = GetScore(word, s, idx);
					matchedWord = word;
					return true;
				}
			}
			return false;
		}

	}
}
