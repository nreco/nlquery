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
	/// Represents generic match for specified key type.
	/// </summary>
	public class KeyMatch<T> : Match {

		/// <summary>
		/// Key value.
		/// </summary>
		public T Key { get; set; }

		public KeyMatch(T key)  {
			Key = key;
		}

		public override string ToString() {
			return "Key["+Convert.ToString(Key)+"]";
		}
	}

	
}
