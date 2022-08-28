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
	/// Composition of several <see cref="IRecognizerState"/> implementations.
	/// </summary>
	public class CompositeMatcher : IMatcher {

		public bool FirstPassOnly => true;
		public bool Recursive => false;

		public IMatcher[] Matchers { get; set; }

		public CompositeMatcher(params IMatcher[] matchers) {
			Matchers = matchers;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			for (int i = 0; i < Matchers.Length; i++) {
				foreach (var m in Matchers[i].GetMatches(matchBag))
					yield return m;
			}
		}


	}
}
