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
	/// Matches exact phrase (all words in specified order).
	/// </summary>
	/// <remarks>This matcher can be used for recognizing phrases or words with special meaning, like  'yesterday', 'next month' etc.</remarks>
	public class ExactPhraseMatcher : IMatcher {

		public bool FirstPassOnly => true;
		public bool Recursive => false;

		string[] Words;

		protected Func<Match> GetMatch;

		/// <summary>
		/// Specifies if <see cref="TokenType.Separator"/> tokens are allowed between phrase words.
		/// </summary>
		public bool AllowSeparators { get; set; } = true;

		public ExactPhraseMatcher(string[] matchWords, Func<Match> getMatch) {
			Words = matchWords;
			GetMatch = getMatch;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			if (Words.Length==1) {
				return MatchSingleWord(matchBag.Statement.Tokens);
			}
			var start = new ExactPhraseMatchState(this);
			return MatchHelper.RunStateMachine(matchBag.Statement.Tokens, start);
		}

		IEnumerable<Match> MatchSingleWord(IEnumerable<Token> tokens) {
			// no need to use states
			var lowerCaseWord = Words[0].ToLower();
			foreach (var t in tokens)
				if ( !String.IsNullOrEmpty(t.Value) && String.Equals(lowerCaseWord, t.Value, StringComparison.OrdinalIgnoreCase)) {
					var m = GetMatch();
					m.Score = Match.ScoreCertain;
					m.Start = t;
					m.End = t;
					yield return m; 
				}
			yield break;
		}

		internal class ExactPhraseMatchState : IMatchState {

			ExactPhraseMatcher Matcher;
			int WordIndex = 0;
			Token Start = null;
			Token End = null;

			internal ExactPhraseMatchState(ExactPhraseMatcher matcher) {
				Matcher = matcher;
			}

			public Match GetResult() {
				if (Start==null || WordIndex < Matcher.Words.Length)
					return null;
				var m = Matcher.GetMatch();
				m.Score = Match.ScoreCertain;
				m.Start = Start;
				m.End = End;
				return m;
			}

			public IEnumerable<IMatchState> Next(Token t) {
				if (Start == null) {
					// propagate start state
					yield return this;
				}

				if (WordIndex >= Matcher.Words.Length)
					yield break; // stop

				if ( !String.IsNullOrEmpty(t.Value) && String.Equals(Matcher.Words[WordIndex], t.Value, StringComparison.OrdinalIgnoreCase)) {
					if (Start == null) {
						var state = new ExactPhraseMatchState(Matcher);
						state.Start = t;
						state.End = t;
						state.WordIndex = 1;
						yield return state;
					} else {
						End = t;
						WordIndex++;
						if (WordIndex < Matcher.Words.Length)
							yield return this;
					}
				} else if (t.Type==TokenType.Separator && Matcher.AllowSeparators) {
					if (WordIndex > 0) {
						// skip space and continue
						yield return this;
					}
				}

			}
		}

	}
}
