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
	/// Represents matcher.
	/// </summary>
	public interface IMatcher {

		/// <summary>
		/// If true matcher is called only once at first pass.
		/// </summary>
		bool FirstPassOnly { get; }

		/// <summary>
		/// If <see cref="FirstPassOnly"/>=false determines if matcher is recursive (executed on every step even if it is already returns some matches).
		/// </summary>
		bool Recursive { get; }

		/// <summary>
		/// Returns new matches by recognition state represented by <see cref="MatchBag"/>.
		/// </summary>
		/// <param name="matchBag">recognition state</param>
		/// <returns>new matches</returns>
		IEnumerable<Match> GetMatches(MatchBag matchBag);
	}
	
}
