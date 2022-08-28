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
	/// Performs 'contains' matching by specified list of values.
	/// </summary>
	public class ListContainsMatcher : IMatcher {

		string[] Values;

		protected Func<ContainsType,KeyValuePair<int, string>, Match> GetMatch;

		public bool FirstPassOnly => true;
		public bool Recursive => false;

		/// <summary>
		/// Determines how many one token matches are allowed before applying max-score filter.
		/// </summary>
		public int MaxScoreFilterThreshold { get; set; } = 10;

		public ListContainsMatcher(string[] values, Func<ContainsType,KeyValuePair<int,string>,Match> getMatch) {
			GetMatch = getMatch;
			Values = values;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			var wordOrNumTokens = matchBag.Statement.Tokens.Where(t=> t.Type == TokenType.Word || t.Type == TokenType.Number).ToArray();
			
			var maxScoreFilterThreshold = MaxScoreFilterThreshold;
			var tokenMatchesCount = new int[wordOrNumTokens.Length];
			var tokenMatchesMaxScore = new float[wordOrNumTokens.Length];
			var currValSkippedMatches = new List<Match>(wordOrNumTokens.Length);

			// contains matches
			for (int i = 0; i < Values.Length; i++) {
				currValSkippedMatches.Clear();
				int valMatchesCount = 0;
				for (int tIdx = 0; tIdx < wordOrNumTokens.Length; tIdx++) {
					var t = wordOrNumTokens[tIdx];
					var val = Values[i];
					var idx = val.IndexOf(t.Value, StringComparison.OrdinalIgnoreCase);
					if (idx >= 0) {
						var contains = ContainsType.Contains;
						if (idx == 0)
							contains = val.Length == t.Value.Length ? ContainsType.Exact : ContainsType.StartsWith;
						var m = GetMatch(contains, new KeyValuePair<int, string>(i, val));
						m.Score = ((float)t.Value.Length) / val.Length;

						// score penalty logic
						if (t.Type == TokenType.Number) {
							// for number
							var nextCharIdx = idx + t.Value.Length;
							var isNumberStart = idx == 0 || !Char.IsLetterOrDigit(val[idx - 1]);
							var isNumberEnd = nextCharIdx>= val.Length || !Char.IsLetterOrDigit(val[nextCharIdx]);
							// penalty if not exact number match
							if (!isNumberStart)
								m.Score /= 2;
							if (!isNumberEnd)
								m.Score /= 2;
						} else {
							// for word: penalty if not beginning of the word
							if (idx > 0 && Char.IsLetterOrDigit(val[idx - 1])) {
								m.Score /= 2; // not word start penalty
							}
						}

						m.Start = t;
						m.End = t;

						valMatchesCount++;
						tokenMatchesCount[tIdx]++;
						bool isNewMaxScore = m.Score > tokenMatchesMaxScore[tIdx];
						if (isNewMaxScore)
							tokenMatchesMaxScore[tIdx] = m.Score;

						// limit number of matches for single token, and return only with HIGHER score after threshold
						if (tokenMatchesCount[tIdx] <= maxScoreFilterThreshold || isNewMaxScore) {
							yield return m;
						} else {
							currValSkippedMatches.Add(m);
						}
					}
				}

				// if value has more than 1 match lets return matches filtered by max score
				if (valMatchesCount > 1 && currValSkippedMatches.Count > 0)
					for (int mIdx = 0; mIdx < currValSkippedMatches.Count; mIdx++)
						yield return currValSkippedMatches[mIdx];
			}
		}

		public enum ContainsType {
			Contains = 0,
			StartsWith = 1,
			Exact = 2
		}
	}
}
