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

namespace NReco.NLQuery.Matchers {
	
	/// <summary>
	/// Merges sequence of similar matches into one.
	/// </summary>
	public class MergePhraseMatcher<T> : IMatcher where T : Match {

		public bool FirstPassOnly => false;
		public bool Recursive => false;

		Func<TokenSequence,T,T,Match> Merge;

		public MergePhraseMatcher(Func<TokenSequence,T, T,Match> merge) {
			Merge = merge;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			if (matchBag.Count == 0)
				yield break;

			var similarMatches = matchBag.Matches
					.Where(m => m is T).Select(m=> (T)m)
					.OrderBy(m => matchBag.Statement.GetIndex(m.Start) ).ToArray();
			var mergedMatches = new HashSet<Match>();
			foreach (var match in similarMatches) {
				if (mergedMatches.Contains(match))
					continue;
				mergedMatches.Add(match); // mark as processed
				var mergedMatch = TryMergeWithNextMatch(match);
				if (mergedMatch != null)
					yield return mergedMatch;
			}

			Match TryMergeWithNextMatch(T m) {
				var endTokenIdx = matchBag.Statement.GetIndex(m.End);
				var tokens = matchBag.Statement.Tokens;
				Match[] nextMatches = null;
				for (int i = endTokenIdx + 1; i < tokens.Length; i++) {
					var t = tokens[i];
					if (t.Type == TokenType.Word || t.Type == TokenType.Number) {
						nextMatches = matchBag.FindByStart(t).ToArray();
						if (nextMatches.Length>0)
							break;
					}
				}
				if (nextMatches == null)
					return null;
				// find continuation
				foreach (var nextMatch in nextMatches)
					if (nextMatch is T nextMatchAsT){
						var mergedMatch = Merge(matchBag.Statement, m, nextMatchAsT);
						if (mergedMatch == null)
							continue;
						mergedMatch.MatchedTokensCount = m.MatchedTokensCount + nextMatchAsT.MatchedTokensCount;
						mergedMatches.Add(nextMatch);
						if (mergedMatch is T mergedMatchAsT) {
							var nextMergedMatch = TryMergeWithNextMatch(mergedMatchAsT);
							if (nextMergedMatch != null)
								return nextMergedMatch;
						}
						return mergedMatch; // that's all
					}
				return null;
			}

		}


	}
}
