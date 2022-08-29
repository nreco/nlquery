using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NReco.NLQuery.Matchers;

namespace NReco.NLQuery.Examples.NerByDataset {

	public class QueryCandidate {

		public Match[] Matches { get; private set; }

		public float Score { get; private set; }

		public TokenSequence SearchQuery { get; private set; }

		public QueryCandidate(Match[] matches, TokenSequence searchQuery) {
			Matches = matches;
			SearchQuery = searchQuery;

			// this is very simple scoring function 
			// sum of all matches weighted by number of matched words or numbers
			var totalWordOrNumCount = searchQuery.Tokens.Where(t => t.Type == TokenType.Word || t.Type == TokenType.Number).Count();
			float totalScore = 0f;
			foreach (var m in matches) {
				totalScore += m.Score * ((float)wordOrNumCount(m)) / totalWordOrNumCount;
			}
			Score = totalScore;

			int wordOrNumCount(Match m) {
				var startTokenIdx = searchQuery.GetIndex(m.Start);
				var endTokenIdx = searchQuery.GetIndex(m.End);
				int cnt = 0;
				Token t;
				for (var i = startTokenIdx; i <= endTokenIdx; i++) {
					t = searchQuery.Tokens[i];
					if (t.Type == TokenType.Word || t.Type == TokenType.Number)
						cnt++;
				}
				return cnt;
			}
		}

		public override string ToString() {
			var sb = new StringBuilder();
			sb.AppendFormat("QueryCandidate: score={0}\n", Score);
			foreach (var m in Matches) {
				var joinedTokens = String.Join("|", SearchQuery.Between(m.Start, m.End).Select(t=>t.Value).ToArray() );
				sb.Append(String.Format("\t{0} score={1} tokens={2}]\n", m.ToString(), m.Score, joinedTokens));
			}
			return sb.ToString();
		}
	}
}
