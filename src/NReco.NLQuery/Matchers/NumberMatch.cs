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
	/// Represents a number match.
	/// </summary>
	public class NumberMatch : Match {

		/// <summary>
		/// Number value.
		/// </summary>
		public decimal Value { get; set; }

		public NumberMatch() {
		}

		public override string ToString() {
			return String.Format(System.Globalization.CultureInfo.InvariantCulture, "Number[{0}]", Value);
		}
	}
	
}
