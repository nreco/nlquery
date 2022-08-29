using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NReco.NLQuery.Examples.NliPivotTable {

	/// <summary>
	/// Implements very simple stemming for English words: handles '-s', '-ed', '-ing'
	/// </summary>
	public class EnglishStemmer {

		public EnglishStemmer() {

		}

		bool IsPluralSuffix(char c) {
			return (c=='p' || c=='b' || c=='g' || c=='k' || c=='t' || c=='d' || c=='r' || c=='n' || c=='l' || c=='v');
		}
		public string Stem(string word) {
			// this is hardcoded heuristics for english words
			// TODO: use wordnet to get correct forms
			if (word.Length>5 && word.EndsWith("ses"))
				return word.Substring(0, word.Length-2); // remove -es
			if (word.Length>3 && word[word.Length-1]=='s' && IsPluralSuffix(word[word.Length-2]) )
				return word.Substring(0, word.Length-1); // remove -s
			if (word.Length > 5 && word.EndsWith("ed")) {
				return word.Substring(0, word.Length-2);
			}
			if (word.Length > 4 && word.EndsWith("ing")) {
				return word.Substring(0, word.Length-3);
			}
			return word;
		}


	}
}
