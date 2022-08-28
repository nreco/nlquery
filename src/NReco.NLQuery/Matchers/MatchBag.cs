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
	/// Holds recognition context (state).
	/// </summary>
	public class MatchBag {

		public TokenSequence Statement { get; private set; }

		List<Match> matches;
		Dictionary<Token, List<Match>> StartToMatches;
		Dictionary<Token, List<Match>> EndToMatches;

		public IEnumerable<Match> Matches {
			get {
				return matches;
			}
		}

		public int Count { get => matches.Count; }

		public MatchBag(TokenSequence statement) : this(statement, null) {

		}

		public MatchBag(TokenSequence statement, IEnumerable<Match> matches) {
			Statement = statement;
			this.matches = new List<Match>();
			StartToMatches = new Dictionary<Token, List<Match>>();
			EndToMatches = new Dictionary<Token, List<Match>>();
			if (matches!=null)
				foreach (var m in matches)
					Add(m);
		}

		public void Add(Match m) {
			matches.Add(m);

			List<Match> l;
			if (!StartToMatches.TryGetValue(m.Start, out l)) {
				l = new List<Match>();
				StartToMatches[m.Start] = l;
			}
			l.Add(m);
			if (!EndToMatches.TryGetValue(m.End, out l)) {
				l = new List<Match>();
				EndToMatches[m.End] = l;
			}
			l.Add(m);

		}

		static readonly Match[] emptyMatchArr = new Match[0];

		public IEnumerable<Match> FindByStart(Token t) {
			if (StartToMatches.TryGetValue(t, out var l))
				return l;
			return emptyMatchArr;
		}

		public IEnumerable<Match> FindByEnd(Token t) {
			if (EndToMatches.TryGetValue(t, out var l))
				return l;
			return emptyMatchArr;
		}

		public IEnumerable<T> Find<T>() where T : Match {
			for (int i = 0; i < matches.Count; i++) {
				var m = matches[i];
				if (m is T)
					yield return (T)m;
			}
		}

	}
}
