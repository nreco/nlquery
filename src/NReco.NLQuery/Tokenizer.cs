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
using System.IO;

namespace NReco.NLQuery {
	
	/// <summary>
	/// Tokenizer parses input search query string into tokens sequence.
	/// </summary>
	public class Tokenizer {

		//public bool StopOnFirstSentense { get; set; }

		public Tokenizer() {
			//StopOnFirstSentense = false;

			//TBD: parse quoted part as single word
		}

		protected virtual IEnumerable<Token> ReadTokens(TextReader rdr) {
			char ch;
			int startIdx = 0;
			int currentPos = 0;
			StringBuilder tokenVal = new StringBuilder();

			var tokenType = TokenType.Unknown;

			var chCode = rdr.Read();
			while (chCode >= 0) {
				ch = (char)chCode;
				if (Char.IsDigit(ch)) {
					var t = checkTokenType(TokenType.Number, false, new[] { TokenType.Word });
					if (t!=null)
						yield return t;
				} else if (IsSeparator(ch) || Char.IsWhiteSpace(ch)) {
					var t = checkTokenType(TokenType.Separator, false, null);
					if (t!=null)
						yield return t;
				} else if (IsBracket(ch)) {
					var t = checkTokenType(TokenType.Bracket, true, null);
					if (t!=null)
						yield return t;
				} else if (IsMath(ch)) {
					var t = checkTokenType(TokenType.Math, true, null);
					if (t!=null)
						yield return t;
				} else if (IsPunctuation(ch)) {
					var t = checkTokenType(TokenType.Punctuation, true, null);
					if (t!=null)
						yield return t;
				} else {
					var t = checkTokenType(TokenType.Word, false, null);
					if (t != null)
						yield return t;
				}

				tokenVal.Append( ch );

				/*if (StopOnFirstSentense && tokenType==TokenType.Punctuation && IsSentenceStop(ch)) {
					chCode = -1;
					break;
				}*/

				chCode = rdr.Read();
				currentPos++;
			}
			var lastToken = checkTokenType(TokenType.Unknown, true, null);
			if (lastToken!=null)
				yield return lastToken;
			if (chCode < 0) { 
				yield return new Token(TokenType.SentenceEnd, startIdx, String.Empty);
			}

			Token checkTokenType(TokenType newTokenType, bool force, TokenType[] allowedTokenTypes) {
				if (!force) {
					if (tokenType == newTokenType)
						return null;
					if (allowedTokenTypes != null)
						for (int i = 0; i < allowedTokenTypes.Length; i++) {
							if (allowedTokenTypes[i] == tokenType)
								return null;
						}
				}
				Token t = null;
				if (tokenVal.Length > 0) {
					t = new Token(tokenType, startIdx, tokenVal.ToString());
					startIdx = currentPos;
					tokenVal.Clear();
				}
				tokenType = newTokenType;
				return t;
			}
		}

		/*protected virtual bool IsSentenceStop(char ch) {
			switch (ch) {
				case '!':
				case '?':
				case '.':
					return true;
			}
			return false;
		}*/

		protected virtual bool IsBracket(char ch) {
			switch (ch) {
				case '(':
				case ')':
				case '[':
				case ']':
				case '{':
				case '}':
					return true;
			}
			return false;
		}

		protected virtual bool IsMath(char ch) {
			switch (ch) {
				case '+':
				case '-':
				case '/':
				case '*':
				case '&':
				case '|':
				case '=':
				case '<':
				case '>':
				case '~':
				case '^':
				case '#':
					return true;
			}
			return false;
		}

		protected virtual bool IsPunctuation(char ch) {
			switch (ch) {
				case ',':
				case ';':
				case ':':
				case '"':
				case '.':
				case '?':
				case '!':
				case '_':
				case '\'':
					return true;
			}
			return false; //Char.IsPunctuation(ch);
		}

		protected virtual bool IsSeparator(char ch) {
			switch (ch) {
				case ' ':
				case '\t':
				case '\n':
				case '\r':
					return true;
			}			
			return false; //Char.IsSeparator(ch);
		}

		/// <summary>
		/// Parses specified string.
		/// </summary>
		/// <param name="s">string value to parse</param>
		/// <returns>sequence of tokens</returns>
		public IEnumerable<Token> Parse(string s) {
			var rdr = new StringReader(s);
			return ReadTokens(rdr);
		}

		/// <summary>
		/// Parses specified string.
		/// </summary>
		/// <param name="rdr">input</param>
		/// <returns>sequence of tokens</returns>
		public IEnumerable<Token> Parse(TextReader rdr) {
			return ReadTokens(rdr);
		}

		/// <summary>
		/// Merges quoted constants into one word-type token.
		/// </summary>
		/// <param name="tokens">tokens to parse</param>
		/// <param name="quoteChar">quote char ('"' by default). This char should correspond to single-char token type (punctuation, math).</param>
		/// <returns>tokens where quoted constants are represented as single word-type tokens</returns>
		public IEnumerable<Token> ParseQuotedConstants(IEnumerable<Token> tokens, char quoteChar = '"') {
			var quotedConst = new StringBuilder();
			bool inQuotedSeq = false;
			int quotedConstStartIdx = -1;
			var tokensEnum = tokens.GetEnumerator();
			while (tokensEnum.MoveNext()) { 
				if (isQuoteToken(tokensEnum.Current)) {
					if (inQuotedSeq) {
						// this is end or escaped quote char?
						if (tokensEnum.MoveNext()) {
							if (isQuoteToken(tokensEnum.Current)) {
								// escaped quote
								quotedConst.Append(tokensEnum.Current.Value);
								continue;
							} else {
								// that was end of quoted constant
								yield return new Token(TokenType.Word, quotedConstStartIdx, quotedConst.ToString());
								reset();
								// and push token that we read
								yield return tokensEnum.Current;
							}
						} else {
							yield return new Token(TokenType.Word, quotedConstStartIdx, quotedConst.ToString());
							reset();
						}
					} else {
						inQuotedSeq = true;
						quotedConstStartIdx = tokensEnum.Current.StartIndex + 1;
					}
				} else {
					if (inQuotedSeq && tokensEnum.Current.Type != TokenType.SentenceEnd) {
						quotedConst.Append(tokensEnum.Current.Value);
					} else {
						if (tokensEnum.Current.Type==TokenType.SentenceEnd && inQuotedSeq) {
							yield return new Token(TokenType.Word, quotedConstStartIdx, quotedConst.ToString());
						}
						yield return tokensEnum.Current;
					}
				}
			}

			bool isQuoteToken(Token t) {
				return t.Value.Length == 1 && t.Value[0] == quoteChar;
			}
			void reset() {
				quotedConst.Clear();
				inQuotedSeq = false;
				quotedConstStartIdx = -1;
			}
		}
	}
}
