using System;
using System.Collections.Generic;
using System.Text;

namespace NReco.NLQuery.Matchers
{
	internal static class MatchHelper { 

		internal static IEnumerable<Match> RunStateMachine(IEnumerable<Token> tokens, IMatchState start) {
			var states = new List<IMatchState>();
			states.Add(start);
			foreach (var t in tokens) {
				var prevStates = states.ToArray();
				states.Clear();
				for (int i = 0; i < prevStates.Length; i++) {
					var prevState = prevStates[i];
					foreach (var nextState in prevState.Next(t)) {
						states.Add(nextState);
					}
					var match = prevState.GetResult();
					if (match != null)
						yield return match;
				}
			}
		}

	}

	internal interface IMatchState {
		IEnumerable<IMatchState> Next(Token t);
		Match GetResult();
	}
}
