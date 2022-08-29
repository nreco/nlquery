using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NReco.NLQuery.Matchers;
using NReco.NLQuery.Table;

namespace NReco.NLQuery.Examples.NlqForOlap {

	/// <summary>
	/// This example illustrates how to parse natural language queries and build OLAP queries (like MDX)
	/// </summary>
	class Program {

		static void Main(string[] args) {

			var schemaComposer = new CubeSchemaComposer();

			// in real-world app you don't need to create new instance of 'Recognizer' each time
			// it can be cached and reused. Matchers shipped with NLQuery are thread-safe.
			// if you use custom matchers ensure that they're thread-safe, 
			// or use pool pattern to guarantee that Recognizer is used only by one thread at time.
			var recognizer = GetRecognizer(schemaComposer.GetSchema());

			ProcessNlq(recognizer, "John sales in Europe");
			Console.WriteLine();
			ProcessNlq(recognizer, "sales by region and rank");

			Console.WriteLine("\nPress any key to exit...");
			Console.ReadKey();
		}

		static void ProcessNlq(Recognizer recognizer, string nlqStr) {
			Console.WriteLine($"NLQ: {nlqStr}");

			var maxResults = 10;

			var tokenizer = new Tokenizer();
			var tokens = tokenizer.Parse(nlqStr).ToArray();

			var statement = new TokenSequence(tokens);
			var topCandidates = new TopSet<QueryCandidate>(maxResults, (a, b) => a.Score.CompareTo(b.Score));

			int processedMatches = 0;
			recognizer.Recognize(statement,
				match => {
					// handle only matches that are signficant for building OLAP query
					return match is ColumnConditionMatch colCndMatch || match is ColumnMatch;
				},
				(matches) => {
					topCandidates.Add(new QueryCandidate(matches, statement));
					processedMatches++;
					if (processedMatches > 1000) // some reasonable limit for number of combinations to process
						return false;
					return true;
				}
			);
			if (topCandidates.Count == 0) {
				Console.WriteLine("No matches. OLAP query cannot be generated.");
			} else {
				var bestCandidate = topCandidates.Max;
				Console.WriteLine(bestCandidate.ToOlapQuery().ToString());
			}
		}

		static Recognizer GetRecognizer(TableSchema tblSchema) {
			var tblMatchBuilder = new TableMatcherBuilder(new TableMatcherBuilder.Options() {
				StopWords = StopWords
			});
			tblMatchBuilder.Add(tblSchema);
			// use tblMatchBuilder.Add if you need to configure additional (custom) matchers
			return new Recognizer(tblMatchBuilder.Build());
		}

		static string[] StopWords = new[] {
			"a", "by", "an", "at", "are", "as", "be", "at", "do","does","did", "etc", "for", "has", "have", "had", "in", "is", "just", "near",
			"of", "on", "per", "the", "to", "vs", "versus", "x", "was",
			"how","many","much","if","it","its","up","so","out",
			"show","about","after",
			"me","i","am","he","his","she","her","any","all","they","their", "them","our","ours",
			"be","been","being","both","but","that","than","could",
			"and", "or", "from", "no", "not"
		};

}
}
