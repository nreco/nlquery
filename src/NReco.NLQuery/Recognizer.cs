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

using NReco.NLQuery.Matchers;

namespace NReco.NLQuery {

	/// <summary>
	/// Recognizes a search query represented by <see cref="TokenSequence"/> with specified list of <see cref="IMatcher"/>s.
	/// </summary>
	public class Recognizer {

		IMatcher[] Matchers;

		/// <summary>
		/// If true recognizer returns all non-matched tokens as <see cref="StubMatch"/>es added to the combination.
		/// </summary>
		public bool IncludeZeroMatches { get; set; } = false;

		/// <summary>
		/// Max number of passes when recognizer tries to consolidate matches.
		/// </summary>
		public int MaxPasses { get; set; } = 100;

		/// <summary>
		/// Initializes a new instance of the <see cref="Recognizer"/>.
		/// </summary>
		/// <param name="matchers">list of matchers</param>
		public Recognizer(params IMatcher[] matchers) {
			Matchers = matchers;
		}

		internal class MatchNode {
			internal MatchNode Prev;
			internal Match Match;
		}

		Dictionary<Token, Match[]> ComposeStartTokenToMatches(IEnumerable<Match> allMatches) {
			var startTokenMatches = new Dictionary<Token, Match[]>();
			foreach (var entry in allMatches.GroupBy(m=>m.Start)) {
				var matches = entry.ToArray();
				Array.Sort(matches, (a, b) => {
					var aLen = (a.End.StartIndex + a.End.Value.Length) - (a.Start.StartIndex);
					var bLen = (b.End.StartIndex + b.End.Value.Length) - (b.Start.StartIndex);
					var cmp = bLen.CompareTo(aLen); // longer first
					if (cmp == 0)
						cmp = b.Score.CompareTo(a.Score); // higher score first
					return cmp;
				});
				startTokenMatches[entry.Key] = matches;
			}
			return startTokenMatches;
		}

		private void ProcessRecursiveMatchers(IMatcher[] recursiveMatchers, MatchBag matchBag) {
			var matchers = new List<IMatcher>(recursiveMatchers.Length);
			matchers.AddRange(recursiveMatchers);

			var matchesList = new List<Match>();
			for (int generation=0; generation < MaxPasses; generation++) {
				int totalMatchesCount = 0;
				var matchersToRun = matchers.ToArray();
				matchers.Clear();
				for (int i=0; i<matchersToRun.Length; i++) {
					var matcher = matchersToRun[i];
					matchesList.Clear();
					foreach (var m in matcher.GetMatches(matchBag)) {
						matchesList.Add(m);
					}
					if (matchesList.Count== 0 || matcher.Recursive) {
						matchers.Add(matcher); // keep for next generation cycle
					}
					for (int j=0; j<matchesList.Count; j++)
						matchBag.Add(matchesList[j]);
					totalMatchesCount += matchesList.Count;
				}
				if (totalMatchesCount == 0) {
					return; // stop
				}
			}
			throw new InvalidOperationException("Too many merge passes, possibly infinite rule");
		}

		/// <summary>
		/// Recognize specified search query and return matches combinations to specified handler.
		/// </summary>
		/// <param name="statement">search query</param>
		/// <param name="combinationHandler">matches combination handler</param>
		public void Recognize(TokenSequence statement, Func<Match[], bool> combinationHandler) {
			Recognize(statement, null, combinationHandler);
		}

		/// <summary>
		/// Recognize specified search query and return filtered combinations of matches to specified handler.
		/// </summary>
		/// <param name="statement">search query</param>
		/// <param name="matchFilter">filter that excludes undesired matches from combinations</param>
		/// <param name="combinationHandler">matches combination handler</param>
		public void Recognize(TokenSequence statement, Func<Match, bool> matchFilter, Func<Match[],bool> combinationHandler) {
			var matchBag = new MatchBag(statement, new Match[0]);
			// first-pass matchers
			foreach (var m in new CompositeMatcher(Matchers.Where(m=>m.FirstPassOnly).ToArray()).GetMatches(matchBag))
				matchBag.Add(m);
			// process recursive matchers
			ProcessRecursiveMatchers(Matchers.Where(m => !m.FirstPassOnly).ToArray(), matchBag);

			var matches = matchBag.Matches;
			if (matchFilter!=null) {
				matches = matches.Where(matchFilter);
			}

			// In some cases 1 token as a lot of single-token matches
			// TBD: use some treshold lets keep only FIRST N higher matches to keep number of combinations reasonable?...

			var startTokenMatches = ComposeStartTokenToMatches(matches);
			var builder = new MatchCombinationBuilder(startTokenMatches, statement, combinationHandler);
			builder.IncludeZeroMatches = IncludeZeroMatches;
			builder.Build();
		}

		internal class MatchCombinationBuilder {
			Dictionary<Token, Match[]> StartToMatches;
			TokenSequence Sentence;
			Func<Match[], bool> ResultHandler;

			internal bool IncludeZeroMatches = false;

			internal MatchCombinationBuilder(Dictionary<Token, Match[]> startToMatches, TokenSequence sentence, Func<Match[], bool> combinationHandler) {
				Sentence = sentence;
				StartToMatches = startToMatches;
				ResultHandler = combinationHandler;
			}

			internal void Build() {
				Traverse(null);
			}

			bool Traverse(MatchNode prevNode) {
				var nextTokenIdx = prevNode != null ? Sentence.GetIndex(prevNode.Match.End) + 1 : 0;
				for (int tIdx = nextTokenIdx; tIdx < Sentence.Tokens.Length; tIdx++) {
					var t = Sentence.Tokens[tIdx];
					if (StartToMatches.TryGetValue(t, out var matches)) {
						for (int mIdx = 0; mIdx < matches.Length; mIdx++)
							if (!Traverse(new MatchNode() { Prev = prevNode, Match = matches[mIdx] }))
								return false;
						// that's all
						return true;
					}
					// no matches for this start, lets try next one
				}
				// there are no more matches. Push current state to callback
				return ResultHandler( (IncludeZeroMatches ? GetAllMatches(prevNode) : GetMatches(prevNode)).ToArray());
			}

			IEnumerable<Match> GetMatches(MatchNode node) {
				while (node != null) {
					yield return node.Match;
					node = node.Prev;
				}
			}

			IEnumerable<Match> GetAllMatches(MatchNode node) {
				var idxToMatch = new Dictionary<int, Match>();
				while (node != null) {
					yield return node.Match;
					idxToMatch[Sentence.GetIndex(node.Match.Start)] = node.Match;
					node = node.Prev;
				}
				var zeroTokens = new List<Token>(Sentence.Tokens.Length);
				for (int i=0; i<Sentence.Tokens.Length; i++) {
					if (idxToMatch.TryGetValue(i, out var m)) {
						var zeroMatch = createZeroMatch();
						if (zeroMatch != null)
							yield return zeroMatch;
						zeroTokens.Clear();
						i = Sentence.GetIndex(m.End);
					} else {
						zeroTokens.Add(Sentence.Tokens[i]);
					}
				}
				var lastZeroMatch = createZeroMatch();
				if (lastZeroMatch!=null)
					yield return lastZeroMatch;

				Match createZeroMatch() {
					if (zeroTokens.Count == 0)
						return null;
					var firstNonSepIdx = -1;
					var lastNonSepIdx = -1;
					for (int i=0; i<zeroTokens.Count; i++)
						if (zeroTokens[i].Type!=TokenType.Separator && zeroTokens[i].Type != TokenType.SentenceEnd) {
							firstNonSepIdx = i;
							break;
						}
					for (int i = zeroTokens.Count - 1; i >=0; i--)
						if (zeroTokens[i].Type != TokenType.Separator && zeroTokens[i].Type != TokenType.SentenceEnd) {
							lastNonSepIdx = i;
							break;
						}
					if (firstNonSepIdx < 0 || lastNonSepIdx < 0)
						return null;

					return new StubMatch() {
						Score = 0f,
						Start = zeroTokens[firstNonSepIdx],
						End = zeroTokens[lastNonSepIdx]
					};
				}
			}


		}


	}
}
