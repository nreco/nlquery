using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.RegularExpressions;

namespace NReco.NLQuery.Examples.NerByDataset {
	
	/// <summary>
	/// Represents MovieLens dataset film data.
	/// </summary>
	public class MovieLensFilm {

		public int ID { get; set; }
		public string Title { get; set; }
		public int? Year { get; set; }
		public string[] Genres { get; set; }

		static Regex splitRegex = new Regex("[:][:]", RegexOptions.Singleline|RegexOptions.Compiled);
		static Regex matchYearRegex = new Regex(@"^(.*)[(]([0-9]{4})[)]\s*$", RegexOptions.Singleline|RegexOptions.Compiled);

		public static MovieLensFilm Parse(string line) {
			var parts = splitRegex.Split(line);
			var movie = new MovieLensFilm();
			movie.ID = Int32.Parse(parts[0]);

			var yearMatch = matchYearRegex.Match(parts[1]);
			if (yearMatch.Success) {
				movie.Title = yearMatch.Groups[1].Value;
				movie.Year = Int32.Parse(yearMatch.Groups[2].Value);
			} else {
				movie.Title = parts[1];
			}
			movie.Genres = parts[2].Split(new []{'|'}, StringSplitOptions.RemoveEmptyEntries);
			return movie;
		}

	}
}
