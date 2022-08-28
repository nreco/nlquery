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
	/// Simple filter that removes stop-words from the tokens sequence.
	/// </summary>
	internal class StopWordsFilter {

		HashSet<string> StopWords;

		public StopWordsFilter(string[] stopWords) {
			StopWords = new HashSet<string>(stopWords.Select(w=>w.ToLower()));
		}

		public bool IsStopWord(string s) {
			return StopWords.Contains(s.ToLower());
		}

		/// <summary>
		/// Removes stop-words from the specified tokens sequence.
		/// </summary>
		/// <param name="tokens">input token sequence</param>
		/// <returns>output sequence without stop-words</returns>
		public IEnumerable<Token> RemoveStopWords(IEnumerable<Token> tokens) {
			foreach (var t in tokens) {
				if (t.Type!=TokenType.Word || !StopWords.Contains(t.ValueLowerCase))
					yield return t;
			}
		}
	}
}
