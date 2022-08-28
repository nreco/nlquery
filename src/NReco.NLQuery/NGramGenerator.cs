/*
 *  Copyright 2016 Vitaliy Fedorchenko (nrecosite.com)
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

namespace NReco.NLQuery {
	
	/// <summary>
	/// Generate n-grams by sequense of elements (tokens).
	/// </summary>
	public class NGramGenerator {

		public int MaxSequenceLength { get; private set; }

		public NGramGenerator() {
			MaxSequenceLength = Int32.MaxValue;
		}

		public NGramGenerator(int maxWords) {
			MaxSequenceLength = maxWords;
		}

		/// <summary>
		/// Generates n-grams grouped by starting token from the specified tokens list.
		/// </summary>
		/// <param name="tokens">List of tokens</param>
		/// <returns>list of phrases grouped by start token index</returns>
		public IEnumerable<Token[]> GenerateNGrams(Token[] tokens) {
			for (int i = 0; i < tokens.Length; i++) {
				foreach (var combination in Generate(tokens, i))
					yield return combination;
			}
		}

		IEnumerable<Token[]> Generate(Token[] tokens, int startIdx) {
			var phraseWords = new List<Token>(tokens.Length);
			for (var i = startIdx; i < tokens.Length && phraseWords.Count<MaxSequenceLength; i++) {
				phraseWords.Add(tokens[i]);
				yield return phraseWords.ToArray();
			}
		}

	}
}
