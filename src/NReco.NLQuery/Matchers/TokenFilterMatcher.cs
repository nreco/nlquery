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
	/// <see cref="IMatcher"/> wrapper that filters tokens by predicate before passing to underlying matcher.
	/// </summary>
	public class TokenFilterMatcher : IMatcher {

		public bool FirstPassOnly => Matcher.FirstPassOnly;
		public bool Recursive => Matcher.Recursive;

		IMatcher Matcher;
		Func<Token, bool> Predicate;

		public TokenFilterMatcher(IMatcher baseMatcher, Func<Token,bool> predicate) {
			Matcher = baseMatcher;
			Predicate = predicate;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			var filteredTokens = matchBag.Statement.Tokens.Where(Predicate).ToArray();
			return Matcher.GetMatches(new MatchBag(new TokenSequence(filteredTokens), matchBag.Matches));
		}


	}
}
