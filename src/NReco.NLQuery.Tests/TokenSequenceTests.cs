using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

using NReco.NLQuery;

namespace NReco.NLQuery.Tests
{

	public class TokenSequenceTests
    {

		string sampleSentence = "show all orders between 2017 mar and 2017 dec.";

		[Fact]
		public void GetIndex() {
			var tokenizer = new Tokenizer();
			var tokens = tokenizer.Parse(sampleSentence).ToArray();
			var tSeq = new TokenSequence(tokens);

			Assert.Equal(0, tSeq.GetIndex(tokens[0]));
			Assert.Equal(4, tSeq.GetIndex(tokens.Where(t=>t.Value=="orders").First() ));
		}
		
		[Fact]
		public void PrevNext() {
			var tokenizer = new Tokenizer();
			var tokens = tokenizer.Parse(sampleSentence).ToArray();
			var tSeq = new TokenSequence(tokens);

			Assert.Equal("all", tSeq.Next(tokens[0], t => t.Type == TokenType.Word).Value);
			Assert.Equal("dec", tSeq.Prev(tokens.Where(t=>t.Value==".").First() ).Value);
		}

	}
}
