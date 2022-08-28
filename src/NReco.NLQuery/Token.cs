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
	/// Represents a token (simplest element of the natural language query).
	/// </summary>
	public class Token {

		/// <summary>
		/// Value of this token.
		/// </summary>
		public string Value { get; private set; }

		/// <summary>
		/// Type of this token.
		/// </summary>
		public TokenType Type { get; private set; }

		/// <summary>
		/// First char index in the original search query string.
		/// </summary>
		public int StartIndex { get; private set; }

		string _ValueLowerCase = null;

		/// <summary>
		/// Lower-case value of this token.
		/// </summary>
		public string ValueLowerCase {
			get {
				return (_ValueLowerCase ?? (_ValueLowerCase = Value.ToLowerInvariant() ) );
			}
		}

		public Token(TokenType tokenType, int startIdx, string value) {
			Value = value;
			Type = tokenType;
			StartIndex = startIdx;
		}

		public override int GetHashCode() {
			return Value.GetHashCode() ^ StartIndex.GetHashCode();
		}

		public override bool Equals(object obj) {
			if (obj is Token) {
				var t = (Token)obj;
				return t.Value==Value && t.StartIndex==StartIndex;
			}
			return false;
		}

		public override string ToString() {
			return Value;
		}

	}

	/// <summary>
	/// Token types that can be parsed by <see cref="Tokenizer"/>.
	/// </summary>
	public enum TokenType {
		Unknown,
		Separator,
		Punctuation,
		Math,
		Bracket,
		Word,
		Number,
		SentenceEnd
	}
}
