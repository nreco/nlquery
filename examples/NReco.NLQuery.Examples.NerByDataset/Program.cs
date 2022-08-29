using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using NReco.NLQuery;
using NReco.NLQuery.Matchers;
using NReco.NLQuery.Table;

namespace NReco.NLQuery.Examples.NerByDataset {

	/// <summary>
	/// This example illustrates how to recognize entities by MovieLens 10M dataset.
	/// </summary>
	class Program {

		static void Main(string[] args) {
			var p = new Program();
			p.Run();
		}

		public void Run() {
			Console.Write("Loading MovieLens films data... ");
			var movieLensFilms = LoadMovieLensFilms();
			Console.WriteLine("Done ({0} films loaded)", movieLensFilms.Count);

			Console.Write("Configuring matchers and recognizer... ");
			var movieLensRecognizer = ConfigureMovieLensRecognizer(movieLensFilms);
			Console.WriteLine("Done");
			Console.WriteLine();

			Console.Write("Recognizing search query: 'child revolution 1996'... ");
			Recognize(movieLensRecognizer, "child revolution 1996"); 

			for (;;) { 
				Console.WriteLine("\nEnter your query (press Enter to exit):");
				var q = Console.ReadLine();
				if (String.IsNullOrWhiteSpace(q))
					break;
				Recognize(movieLensRecognizer, q);
			}
		}

		/// <summary>
		/// Recognize specified query and get match results for further analysis.
		/// </summary>
		void Recognize(Recognizer recognizer, string query) {
			var tokenizer = new Tokenizer();
			var tokens = tokenizer.Parse(query);

			var searchQuery = new TokenSequence(tokens.ToArray());

			// result is a list of all possible phrase combinations that match at least something
			// match type and score depends on the matcher implementation

			// in the real NLI application combinations should be analyzed and scored with domain-specific relevance function.
			// best match(es) are converted into the formal query like SQL (or, maybe, to system's action).

			var topCandidates = new TopSet<QueryCandidate>(5, (a, b) => a.Score.CompareTo(b.Score));
			int processedMatches = 0;
			recognizer.IncludeZeroMatches = true;
			recognizer.Recognize(searchQuery,
				// handle only matches that are significant for scoring function in QueryCandidate
				match => match is ColumnMatch || match is ColumnConditionMatch,

				(matches) => {
					topCandidates.Add(new QueryCandidate(matches, searchQuery));
					processedMatches++;
					if (processedMatches > 1000) // some reasonable limit for number of combinations to process
						return false;
					return true;
				}
			);

			Console.WriteLine("Processed {0} phrase combinations", processedMatches);
			foreach (var candidate in topCandidates.ToArray()) {
				Console.WriteLine(candidate.ToString());
			}

		}

		/// <summary>
		/// Configure a matcher that uses MovieLens data to match an entity.
		/// </summary>
		Recognizer ConfigureMovieLensRecognizer(IList<MovieLensFilm> films) {
			var filmTableSchema = new TableSchema() {
				Name = "movielens",
				Caption = "Films",
				Columns = new ColumnSchema[] {
					new ColumnSchema() {
						Caption = "Title", Name = "Title", DataType = TableColumnDataType.String,
						Values = films.Select(f=>f.Title)
							.Take(5000)  // NLQuery trial mode limitation: max 5000 values
							.ToArray()
					},
					new ColumnSchema() {
						Caption = "Genres", Name="Genres", DataType = TableColumnDataType.String,
						Values = films.SelectMany(f=>f.Genres).Distinct().ToArray()
					},
					new ColumnSchema() {
						Caption = "Year", Name="Year", DataType = TableColumnDataType.Number,
						Values = films.Where(f=>f.Year.HasValue).Select(f=>f.Year.ToString()).Distinct().ToArray()
					}

				}
			};

			var tblMatchBuilder = new TableMatcherBuilder(new TableMatcherBuilder.Options() {
				StopWords = EnglishStopWords
			});
			tblMatchBuilder.Add(filmTableSchema);
			return new Recognizer(tblMatchBuilder.Build());
		}

		IList<MovieLensFilm> LoadMovieLensFilms() {
			var res = new List<MovieLensFilm>();
			using (var fs = new FileStream("movies.dat", FileMode.Open, FileAccess.Read)) {
				using (var rdr = new StreamReader(fs)) {
					for (;;) {
						var s = rdr.ReadLine();
						if (String.IsNullOrEmpty(s)) {
							break;
						} else {
							res.Add( MovieLensFilm.Parse(s) );
						}
					}
				}
			}
			return res;
		}

		static string[] EnglishStopWords = new[] {
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
