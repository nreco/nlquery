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
	/// Represents a match which describes one or more tokens from original search query.
	/// </summary>
	public abstract class Match {

		/// <summary>
		/// Measure that reflects match score (0..1).
		/// </summary>
		/// <remarks>
		/// Strict matches (100% hit) should have Score=1. Matchers that perform fuzzy search may return a value between 0 and 1 
		/// that reflects subjective score of the match.
		/// </remarks>
		public float Score { get; set; } = 0.0f;

		/// <summary>
		/// Start token.
		/// </summary>
		public Token Start { get; set; }

		/// <summary>
		/// End token. Can be equal to <see cref="Start"/> if only one token is matched.
		/// </summary>
		public Token End { get; set; }

		public Match() {
		}

		public virtual Match Clone() {
			return (Match)MemberwiseClone();
		}

		public const float ScoreMaybe = 0.5f;
		public const float ScoreCertain = 1.0f;
	}

	/// <summary>
	/// <see cref="StubMatch"/> used to wrap unrecognized token(s).
	/// </summary>
	public class StubMatch : Match {

		public override string ToString() {
			var val = Start.Value;
			if (Start != End)
				val += ".." + End.Value;
			return $"StubMatch[{val}]";
		}
	}

}
