using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

using NReco.NLQuery;

namespace NReco.NLQuery.Tests
{

	public class NGramGeneratorTests
    {

		[Fact]
		public void GenerateNGram() {

			var words = new Token[] {
				new Token(TokenType.Word, 0, "the"),
				new Token(TokenType.Word, 10, "president"),
				new Token(TokenType.Word, 20, "of"),
				new Token(TokenType.Word, 30, "world")
			};

			var nGramGenerator = new NGramGenerator(3);
			var res = nGramGenerator.GenerateNGrams(words).ToArray();

			Assert.Equal(9, res.Length);
			Assert.Equal("the|the president|the president of|president|president of|president of world|of|of world|world", 
				String.Join("|", res.Select(tokens=> String.Join(" ", tokens.Select(t=>t.Value).ToArray() ) ) ) );

		}

    }
}
