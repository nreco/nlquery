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
	/// Matches a number.
	/// </summary>
	public class NumberMatcher : IMatcher {

		public bool FirstPassOnly => true;
		public bool Recursive => false;

		public NumberMatcher() {
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			var start = new NumberMatchState();
			return MatchHelper.RunStateMachine(matchBag.Statement.Tokens, start);
		}

		internal class NumberMatchState : IMatchState {
			Token Start = null;
			Token End = null;

			internal NumberMatchState() {
			}

			public Match GetResult() {
				if (Start==null || End==null || End.Type!=TokenType.Number)
					return null;
				var m = new NumberMatch();
				m.Score = Match.ScoreCertain;
				m.Start = Start;
				m.End = End;

				var numStr = Start.Value;
				if (Start!=End)
					numStr += "." + End.Value;
				m.Value = Decimal.Parse(numStr, System.Globalization.CultureInfo.InvariantCulture);
				return m;
			}

			public IEnumerable<IMatchState> Next(Token t) {
				if (Start == null) {
					// propagate start state
					yield return this;
				}
				switch (t.Type) {
					case TokenType.Punctuation:
						if (Start!=null && Start==End && (t.Value=="." || t.Value==",")) {
							// consume dot or comma (decimal separators) and continue
							yield return new NumberMatchState() {
								Start = Start,
								End = t
							};
						}
						break;
					case TokenType.Number:
						if (Start == null) {
							var state = new NumberMatchState();
							state.Start = t;
							state.End = t;
							yield return state;
						} else if (End.Type==TokenType.Punctuation) {
							End = null;
							yield return new NumberMatchState() {
								Start = Start,
								End = t
							};
						}
						break;
				}

			}
		}

	}
}
