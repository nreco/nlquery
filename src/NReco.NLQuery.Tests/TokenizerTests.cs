using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

using NReco.NLQuery;

namespace NReco.NLQuery.Tests
{

	public class TokenizerTests
    {

		string[][] sentenceTestData = new [] {
			new [] { "What is this?", "Word,Separator,Word,Separator,Word,Punctuation,SentenceEnd"},
			new [] { "John  is 5 years old.", "Word,Separator,Word,Separator,Number,Separator,Word,Separator,Word,Punctuation,SentenceEnd" },
			new [] { "sales (total/5000)*100 by year ", "Word,Separator,Bracket,Word,Math,Number,Bracket,Math,Number,Separator,Word,Separator,Word,Separator,SentenceEnd" },
			new [] { "Some day: 5 Jan 2007", "Word,Separator,Word,Punctuation,Separator,Number,Separator,Word,Separator,Number,SentenceEnd"},
			new [] { "05-07-2012", "Number,Math,Number,Math,Number,SentenceEnd"},
			new [] { "211200159_2_211200167", "Number,Punctuation,Number,Punctuation,Number,SentenceEnd" },
			new [] { "C#, java; _underscore", "Word,Punctuation,Separator,Word,Punctuation,Separator,Punctuation,Word,SentenceEnd" },
			new [] { "num1>5|num2=7.2", "Word,Math,Number,Math,Word,Math,Number,Punctuation,Number,SentenceEnd" },
			new [] { "aa555 5aaa", "Word,Separator,Number,Word,SentenceEnd" }
		};


		[Fact]
		public void ParseSentence() {

			var tokenizer = new Tokenizer();

			foreach (var testData in sentenceTestData) {
				var sentence = testData[0];
				var expectedTokenTypes = testData[1];

				var tokens = tokenizer.Parse(sentence).ToArray();

				Assert.Equal(sentence, String.Join("", tokens.Select(t=>t.Value).ToArray() ) );
				Assert.Equal(expectedTokenTypes, String.Join(",", tokens.Select(t=>t.Type.ToString()).ToArray() ));
			}

			// sentence-only test


		}

		[Fact]
		public void Token() {
			var t = new Token(TokenType.Word, 0, "John");

			Assert.Equal(TokenType.Word, t.Type);
			Assert.Equal(0, t.StartIndex);
			Assert.Equal("john", t.ValueLowerCase);

			var t2 = new Token(TokenType.Separator, 0, " ");
			Assert.False( t.Equals(t2) );
			Assert.False( t2.Equals(t) );

			var t3 = new Token(TokenType.Word, 0, "John");
			Assert.True( t3.Equals(t) );
			Assert.True( t.Equals(t3) );
		}

		[Fact]
		public void Phrase() {

			var sentence = new Tokenizer().Parse("Terminator 2: Judgment Day (1991)").ToArray();
            var words = sentence.Where(t => t.Type==TokenType.Word || t.Type==TokenType.Number).ToArray();

			var s = new TokenSequence(sentence);
			var p = new TokenSequence( words );
			Assert.Equal("terminator 2 judgment day 1991", String.Join(" ",p.Tokens.Select(t=>t.ValueLowerCase).ToArray()) );
			Assert.Equal("terminator", p.FirstToken.ValueLowerCase);
			Assert.Equal("1991", p.LastToken.ValueLowerCase);

			// token indexes
			Assert.Equal(0, p.FirstToken.StartIndex);
			Assert.Equal(11, p.Tokens[1].StartIndex);
			Assert.Equal(28, p.LastToken.StartIndex);

			// distance
			Assert.Equal(1, p.Distance( words[0], words[1] ) );
			Assert.Equal(4, p.Distance( words[0], p.LastToken ) );

			// next/prev
			Assert.Equal(" ", s.Next(words[0], null).ToString() );
			Assert.Equal("Judgment", s.Next(words[0], t => t.Type == TokenType.Word).ToString() );
			Assert.Equal("2", s.Next(words[0], t => t.Type == TokenType.Number).ToString());
			Assert.Null( s.Next( sentence[sentence.Length-1], null ) );

			var w1991 = words.Where(w => w.Value == "1991").First();
			Assert.Equal("Day", s.Prev(w1991, t => t.Type == TokenType.Word).ToString());
			Assert.Equal(")", s.Next(w1991, null).ToString() );

			// between
			Assert.Equal(": ", String.Concat(s.Between( words[1], words[2], false).Select(t=>t.Value).ToArray()  ) );
			Assert.Equal("2: Judgment", String.Concat(s.Between(words[1], words[2]).Select(t => t.Value).ToArray()));
			Assert.Empty(s.Between(words[2], words[1], false));
		}

		[Fact]
		public void QuotedConstants() {
			var tokenizer = new Tokenizer();

			var testInputs = new[] {
				"Hello \"World\"",
				" \"That's \"\"ok\"\"!\" A",
				"A \"B ",
				"\"A A\" \"B B\""
			};
			var testOutputs = new[] {
				"Word[Hello]Separator[ ]Word[World]SentenceEnd[]",
				"Separator[ ]Word[That's \"ok\"!]Separator[ ]Word[A]SentenceEnd[]",
				"Word[A]Separator[ ]Word[B ]SentenceEnd[]",
				"Word[A A]Separator[ ]Word[B B]SentenceEnd[]"
			};
			for (int i=2; i<testInputs.Length; i++) {
				var tokens = tokenizer.ParseQuotedConstants(tokenizer.Parse(testInputs[i]));
				var str = String.Concat(tokens.Select(t => t.Type.ToString()+"["+t.Value+"]" ).ToArray());
				Assert.Equal(testOutputs[i], str);
			}
		}

    }
}
