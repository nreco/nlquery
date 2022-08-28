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
	/// Represents the sequence of tokens (parsed search query).
	/// </summary>
	public class TokenSequence {

		public Token[] Tokens { get; private set; }

		public Token FirstToken {
			get {
				if (Tokens.Length>0)
					return Tokens[0];
				return null;
			}
		}

		public Token LastToken {
			get {
				if (Tokens.Length>0)
					return Tokens[Tokens.Length-1];
				return null;
			}
		}

		public TokenSequence(params Token[] tokens) {
			Tokens = tokens;
		}

		Dictionary<Token, int> tokenToIndex = null;

		public int GetIndex(Token t) {
			if (tokenToIndex==null) {
				tokenToIndex = new Dictionary<Token, int>();
				for (int i = 0; i < Tokens.Length; i++)
					tokenToIndex[Tokens[i]] = i;
			}
			if (tokenToIndex.TryGetValue(t, out var idx))
				return idx;
			return -1;
		}

		public Token Next(Token t, Func<Token,bool> predicate = null) {
			var i = GetIndex(t);
			if (i>=0) {
				for (int j = (i + 1); j < Tokens.Length; j++)
					if (predicate == null || predicate(Tokens[j]))
						return Tokens[j];
				return null;
			}
			return null;
		}

		public Token Prev(Token t, Func<Token, bool> predicate = null) {
			var i = GetIndex(t);
			if (i>=0) {
				for (int j = (i - 1); j >=0; j--)
					if (predicate == null || predicate(Tokens[j]))
						return Tokens[j];
				return null;
			}
			return null;
		}

		public IEnumerable<Token> Between(Token t1, Token t2, bool inclusive = true) {
			int startIdx = GetIndex(t1);
			int endIdx = GetIndex(t2);
			if (startIdx < 0 || endIdx < 0)
				yield break;
			if (!inclusive) {
				startIdx++;
				endIdx--;
			}
			for (int i = startIdx; i <= endIdx; i++)
				yield return Tokens[i];
		}

		/// <summary>
		/// Returns distrance between tokens in the phrase.
		/// </summary>
		/// <param name="t1">first token</param>
		/// <param name="t2">second token</param>
		/// <returns>distance between tokens or -1</returns>
		public int Distance(Token t1, Token t2) {
			int startIdx = GetIndex(t1);
			int endIdx = GetIndex(t2);
			if (startIdx < 0 || endIdx < 0)
				return -1;
			return Math.Abs(endIdx - startIdx);
		}

		public override string ToString() {
			var sb = new StringBuilder();
			for (int i = 0; i < Tokens.Length; i++)
				sb.Append(Tokens[i].Value);
			return sb.ToString();
		}
	}

}
