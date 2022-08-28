using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

using NReco.NLQuery;
using NReco.NLQuery.Matchers;
using NReco.NLQuery.Table;

namespace NReco.NLQuery.Tests
{

	public class RecognizerTests
    {

		protected Recognizer GetSimpleRecognizer() {
			var r = new Recognizer(
				new DateMatcher(),
				new NumberMatcher(),
				new LikePhraseMatcher(new[] { "John", "Smith" }, ()=> new KeyMatch<string>("name") ),
				new LikePhraseMatcher(new[] { "Johnny", "Mmnemonic" }, ()=> new KeyMatch<string>("film") )
			);
			return r;
		}

		[Fact]
		public void CheckCombinations() {
			var r = GetSimpleRecognizer();
			var tSeq = new TokenSequence(new Tokenizer().Parse("show John tasks from 08.2017").ToArray() );
			var cnt = 0;
			var matchedAsName = false;
			var matchedAsFilm = false;
			r.Recognize(tSeq, (matches) => {
				var s = String.Join(" ", matches.Select(m => m.ToString()).ToArray());
				if (s == "Date[Y:2017 M:8] Key[name]")
					matchedAsName = true;
				if (s == "Date[Y:2017 M:8] Key[film]")
					matchedAsFilm = true;
				cnt++;
				return true;
			});
			Assert.True(matchedAsName);
			Assert.True(matchedAsFilm);
			Assert.Equal(8, cnt);
		}

		[Fact]
		public void TableRecognizerTest() {
			var tblSchema = new TableSchema() {
				Caption = "Orders",
				Name = "orders",
				Columns = new[] {
					 new ColumnSchema() {
						 Caption = "Product",
						 Name = "product_name",
						 DataType = TableColumnDataType.String,
						 Values = new[] { "Bud 6pcs", "Krusovice 0.5l"}
					 },
					 new ColumnSchema() {
						 Caption = "Customer",
						 Name = "customer",
						 DataType = TableColumnDataType.String
					 },
					 new ColumnSchema() {
						 Caption = "Country",
						 Name = "country",
						 DataType = TableColumnDataType.String,
						 Values = new[] {"Italy", "France", "USA", "Canada"}
					 },
					 new ColumnSchema() {
						 Caption = "Placed Date",
						 Name = "placed_date",
						 DataType = TableColumnDataType.Date
					 },
					 new ColumnSchema() {
						 Caption = "Shipped Date",
						 AltCaptions = new[]{"Delivered Date"},
						 Name = "shipped_date",
						 DataType = TableColumnDataType.Date
					 },
					 new ColumnSchema() {
						 Caption = "Internal ID",
						 Name = "id",
						 DataType = TableColumnDataType.String,
						 ExactMatchOnly = true
					 },
					 new ColumnSchema() {
						 Caption = "super_id",
						 Name = "super_id",
						 DataType = TableColumnDataType.String,
						 ExactMatchOnly = true
					 },
					 new ColumnSchema() {
						 Caption = "value",
						 Name = "value",
						 DataType = TableColumnDataType.Number,
						 ExactMatchOnly = false
					 }
				 }
			};
			var tableMatchBuilder = new TableMatcherBuilder().Add(tblSchema);
			var recognizer = new Recognizer(tableMatchBuilder.Build());
			var tokenizer = new Tokenizer();

			var testInputs = new[] {
				"show customer order from Italy placed yesterday",
				"customer Krusovice internal",
				"internal id 5",
				"internal id A5",
				"super_id A5, super _id, super _ id",
				"delivered"			};
			var expectedOutput = new[] {
				"Column[placed_date exact 'DateOffset[Y:0 M:0 D:-1]'],Column[country exact 'Italy'],Table[orders],Column[customer]|"+
				"DateOffset[Y:0 M:0 D:-1],Column[placed_date],Column[country exact 'Italy'],Table[orders],Column[customer]",

				"Column[product_name startswith 'Krusovice' in 'Krusovice 0.5l'],Column[customer]",

				"Number[5],Column[id]|Column[product_name contains '5' in 'Krusovice 0.5l'],Column[id]",

				"Column[id contains 'StubMatch[A5]']|Column[id]",

				"Column[super_id contains 'StubMatch[A5]']|Column[super_id]",

				"Column[shipped_date]"
			};
			for (int i = 0; i < testInputs.Length; i++) {
				var p = new TokenSequence(tokenizer.Parse(testInputs[i]).ToArray());

				var combinations = new List<Match[]>();
				recognizer.Recognize(p, (matches) => {
					combinations.Add(matches);
					return true;
				});
				var output = String.Join("|", combinations.Select(comb => String.Join(",", comb.Select(m => m.ToString()).ToArray())).ToArray());
				Assert.Equal(expectedOutput[i], output);
			}

			var testInputsComplex = new[] {
				"value = 1 or value<0 or val>10 or val=1000",
				" shipped = 1 May 2019 or placed >= 30 Apr 2019 ",
				"val=1 and val=2 or val=3",
				"delivered before 1 May"
			};
			var expectedOutputContainsCombinations = new[] {
				"Group[Or:Group[Or:Column[value equal 'Number[1]'];Column[value lessthan 'Number[0]']];Group[Or:Column[value greaterthan 'Number[10]'];Column[value equal 'Number[1000]']]]",
				"Group[Or:Column[shipped_date equal 'Date[Y:2019 M:5 D:1]'];Column[placed_date greaterthanorequal 'Date[Y:2019 M:4 D:30]']]",
				"Group[Or:Group[And:Column[value equal 'Number[1]'];Column[value equal 'Number[2]']];Column[value equal 'Number[3]']]",
				"Column[shipped_date lessthan 'Date[M:5 D:1]']"
			};
			for (int i = 3; i < testInputsComplex.Length; i++) {
				var p = new TokenSequence(tokenizer.Parse(testInputsComplex[i]).ToArray());

				var expectedOuputFound = false;
				recognizer.Recognize(p, (matches) => {
					var combinationStr = String.Join(",", matches.Select(m => m.ToString()).ToArray());
					if (combinationStr==expectedOutputContainsCombinations[i]) {
						expectedOuputFound = true;
						return false;
					}
					return true;
				});
				Assert.True(expectedOuputFound, $"Combination not found for inputComplex #{i}");
			}


		}

	}
}
