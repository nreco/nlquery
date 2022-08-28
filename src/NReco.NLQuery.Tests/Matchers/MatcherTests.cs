using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

using Xunit;
using Xunit.Abstractions;
using NReco.NLQuery;
using NReco.NLQuery.Matchers;
using NReco.NLQuery.Table;

namespace NReco.NLQuery.Tests
{
    public class MatcherTests
    {
		ITestOutputHelper Output;

		public MatcherTests(ITestOutputHelper output) {
			Output = output;
		}

		[Fact]
		public void ListContainsMatcher() {
			var tokenizer = new Tokenizer();
			var matcher = new ListContainsMatcher(
				new[]{
					"Paris", "New York", "Kiev", "London", "Part 2", "2nd quarter", "up2you"
				}, (containsType,matchedVal)=>new KeyMatch<string>("city") );

			var testInputs = new[] {
				"sales in Paris and New York",
				"New Yorker by product",
				"2"
			};
			var expectedOutput = new[] { 3, 1, 3 };
			var expectedScoreSum = new[] { 1.0f+0.875f, 0.375f, 1f/6+ (1f/11)/2 + (1f/6)/4 };
			for (int i = 0; i < testInputs.Length; i++) {
				var p = new TokenSequence(tokenizer.Parse(testInputs[i]).ToArray());
				var matches = matcher.GetMatches(new MatchBag(p)).ToArray();
				Assert.Equal(expectedOutput[i], matches.Length);
				Assert.Equal(expectedScoreSum[i], matches.Sum(m => m.Score));
			}

			// test max score filter
			var similarVals = new List<string>();
			var curVal = "";
			for (int i=0; i<100; i++) {
				curVal += "2";
				similarVals.Add(curVal);
			}
			Assert.Equal(10, 
				new ListContainsMatcher(similarVals.ToArray(), (containsType, matchedVal) => new StubMatch())
				.GetMatches(new MatchBag(new TokenSequence(tokenizer.Parse("2").ToArray()))).Count() );
			// test for exception when 2 tokens matched
			similarVals.Add("222222222222222222222222222 a");
			Assert.Equal(11+1,
				new ListContainsMatcher(similarVals.ToArray(), (containsType, matchedVal) => new StubMatch())
				.GetMatches(new MatchBag(new TokenSequence(tokenizer.Parse("2 a").ToArray()))).Count());

		}

		[Fact]
		public void NumberMatcher() {
			var tokenizer = new Tokenizer();
			var matcher = new NumberMatcher();

			var testInputs = new[] {
				"no numbers", "1", "20.09", "jan 0270 test", "average 20,5 bla 5., ,6"
			};
			var expectedOutput = new[] {
				"",
				"Number[1]",
				"Number[20],Number[9],Number[20.09]",
				"Number[270]",
				"Number[20],Number[5],Number[20.5],Number[5],Number[6]"
			};
			for (int i = 0; i < testInputs.Length; i++) {
				var p = new TokenSequence(tokenizer.Parse(testInputs[i]).ToArray());
				var matches = matcher.GetMatches(new MatchBag(p)).ToArray();
				Assert.Equal(expectedOutput[i], String.Join(",", matches.Select(m => m.ToString()).ToArray()));
			}
		}

		[Fact]
		public void DateMatcherTest() {
			var tokenizer = new Tokenizer();
			var matcher = new DateMatcher();
			matcher.DateFormat = CultureInfo.GetCultureInfo("en-US").DateTimeFormat;

			var testInputs = new [] {
				"19 march 2018",
				"1", "20.09", "jan", "Feb", "March", "September", "50", "08 2007", "show May, 6 2017", "from 2/7/2015 to",
				"before Dec-2017 and"
			};
			var expectedOutput = new [] {
				"Date[Y:2018],Date[Y:2018 M:3],Date[Y:2018 M:3 D:19]",
				"",
				"Date[M:9 D:20]",
				"Date[M:1]",
				"Date[M:2]",
				"Date[M:3]",
				"Date[M:9]",
				"",
				"Date[Y:2007],Date[Y:2007 M:8]",
				"Date[Y:2017],Date[Y:2017 M:6],Date[Y:2017 M:5 D:6]",
				"Date[Y:2015],Date[Y:2015 M:7],Date[Y:2015 M:2 D:7],Date[Y:2015 M:7 D:2]",
				"Date[Y:2017],Date[Y:2017 M:12]",
			};
			for (int i = 0; i < testInputs.Length; i++) {
				var p = new TokenSequence( tokenizer.Parse(testInputs[i]).ToArray() );
				var matches = matcher.GetMatches(new MatchBag(p)).ToArray();
				Assert.Equal(expectedOutput[i], String.Join(",",matches.Select( m=>m.ToString()).ToArray()) );
			}
		}

		[Fact]
		public void ExactPhraseMatcherTest() {

			var tokenizer = new Tokenizer();

			var matcher = new CompositeMatcher(
				new ExactPhraseMatcher(new[] { "tomorrow" }, () => new DateOffsetMatch()),
				new ExactPhraseMatcher(new[] { "last", "month" }, () => new DateOffsetMatch())
			);

			var testInputs = new[] {
				"show me tomorrow activities",
				"last month",
				"clients registered last month",
				"living for tomorr ow"
			};
			var expectedOutput = new[] {
				1,
				1,
				1,
				0
			};

			for (int i = 0; i < testInputs.Length; i++) {
				var p = new TokenSequence(tokenizer.Parse(testInputs[i]).ToArray());
				var matches = matcher.GetMatches(new MatchBag(p)).ToArray();
				Assert.Equal(expectedOutput[i], matches.Length);
			}
		}

		[Fact]
		public void LikePhraseMatcherTest() {
			var tokenizer = new Tokenizer();
			var matcher = new LikePhraseMatcher(new[] { "sum", "of", "sales" }, () => new DateOffsetMatch()) {
				ScoreWeightByTotalLength = true
			};

			var testInputs = new[] {
				"sale by year",
				"show sum of sale as table",
				"state ca, sales sum",
				"summer salt",
				" sum sales sales",
				"good pale ale"
			};
			var expectedOutput = new[] { 1, 1, 1, 0, 2, 1 };
			var expectedScoreSum = new[] { 0.4f, 0.90f, 0.8f, 0, 0.8f+0.5f, 0.15f };
			for (int i = 0; i < testInputs.Length; i++) {
				var p = new TokenSequence(tokenizer.Parse(testInputs[i]).ToArray());
				var matches = matcher.GetMatches(new MatchBag(p)).ToArray();
				Assert.Equal(expectedOutput[i], matches.Length);
				Assert.Equal(expectedScoreSum[i], matches.Sum(m=>m.Score));
			}

		}

		[Fact]
		public void HintMergeRuleTest() {
			var tokenizer = new Tokenizer();
			var hintMatcher = new LikePhraseMatcher(new[] { "From", "City" }, new KeyMatch<int>(1).Clone );
			var valueMatcher = new ListContainsMatcher(
					new[] { "Kiev", "Rome", "Berlin", "New York", "Vatican" }, 
					(containsType, matchVal) => new KeyMatch<string>("city").Clone() );
			var valueAnotherMatcher = new ListContainsMatcher(
					new[] { "France", "Germany", "Vatican" }, 
					(containsType, matchVal) => new KeyMatch<string>("country").Clone() );
			var matcher = new CompositeMatcher(hintMatcher, valueMatcher, valueAnotherMatcher);
			var mergeRule = new HintMatcher<KeyMatch<int>>( (hint,target,force) => {
				if (target is KeyMatch<string> keyM) {
					if (keyM.Key == "city" || force)
						return new KeyMatch<KeyValuePair<int, string>>(new KeyValuePair<int, string>(1, "city"));
				}
				if (target is StubMatch) {
					return new KeyMatch<KeyValuePair<int, string>>(new KeyValuePair<int, string>(1, "city"));
				}
				return null;
			});

			var testInputs = new[] {
				"product1 in city Paris or Kiev",
				"show city Vatican or Germany ",
				"test negative city France and",
				"test force city: France"
			};
			var expectedOutputCount = new[] { 1, 1, 0, 1 };
			var expectedScoreSum = new[] { 0.5f, 0.75f, 0f, 0.75f };
			for (int i = 0; i < testInputs.Length; i++) {
				var p = new TokenSequence(tokenizer.Parse(testInputs[i]).ToArray());
				var matches = matcher.GetMatches(new MatchBag(p)).ToArray();
				var mergedMatches = mergeRule.GetMatches(new MatchBag(p, matches)).ToArray();
				Assert.Equal(expectedOutputCount[i], mergedMatches.Length);
				Assert.Equal(expectedScoreSum[i], mergedMatches.Sum(m => m.Score));
			}
		}

		[Fact]
		public void ComparisonAndGroupTest() {
			var tokenizer = new Tokenizer();
			var valueMatcher = new ListContainsMatcher(
					new[] { "city", "country", "population"},
					(containsType, matchVal) => new KeyMatch<string>(matchVal.Value).Clone());
			var matcher = new CompositeMatcher(valueMatcher, new NumberMatcher());
			var comparisonMatcher = new ComparisonMatcher(
				(left) => left is KeyMatch<string> km,
				(left,cmp,right) => {
					if (right is NumberMatch && left is KeyMatch<string> km && km.Key == "population")
						return new ComparisonMatch(left, cmp, right);
					if (left is KeyMatch<string>)
						return new ComparisonMatch(left, cmp, right);
					return null;
				}
			);
			comparisonMatcher.PhraseComparisonTypes = new[] {
				new KeyValuePair<string[],ComparisonMatcher.ComparisonType>(new[]{"greater", "than"}, ComparisonMatcher.ComparisonType.GreaterThan),
				new KeyValuePair<string[],ComparisonMatcher.ComparisonType>(new[]{"greater"}, ComparisonMatcher.ComparisonType.GreaterThan),
				new KeyValuePair<string[],ComparisonMatcher.ComparisonType>(new[]{"before"}, ComparisonMatcher.ComparisonType.LessThan),
			};
			var groupMatcher = new GroupMatcher(
				(left, matchBag) => (left is ComparisonMatch || left is KeyMatch<string>),
				(left,cmp,right, matchBag) => {
					if ( (right is ComparisonMatch || right is KeyMatch<string>) && !GroupMatch.IsAlreadyInGroup(matchBag, left, right)) {
						return new GroupMatch(cmp, left, right);
					}
					return null;
				}
			);
			groupMatcher.PhraseGroupTypes = new[] {
				new KeyValuePair<string[],GroupMatcher.GroupType>(new[]{"and"}, GroupMatcher.GroupType.And),
				new KeyValuePair<string[],GroupMatcher.GroupType>(new[]{"or"}, GroupMatcher.GroupType.Or)
			};
			var testInputs = new[] {
				"city and popul > 10",
				"city=Kiev or city =Berlin or city bla",
				"population greater than 1000",
				"population greater 100 city=Paris",
				"population before "
			};
			var expectedComparisonOutputs = new[] {
				"Key[population][GreaterThan]Number[10]",
				"Key[city][Equal]StubMatch[Kiev];Key[city][Equal]StubMatch[Berlin]",
				"Key[population][GreaterThan]Number[1000]",
				"Key[city][Equal]StubMatch[Paris];Key[population][GreaterThan]Number[100]",
				""
			};
			var expectedGrpOutputs = new[] {
				"Group[And:Key[city];Key[population]];Group[And:Key[city];Key[population][GreaterThan]Number[10]]",
				"Group[Or:Key[city][Equal]StubMatch[Kiev];Key[city]];Group[Or:Key[city][Equal]StubMatch[Kiev];Key[city][Equal]StubMatch[Berlin]];Group[Or:Key[city][Equal]StubMatch[Berlin];Key[city]]",
				"", "", ""
			};
			for (int i = 0; i < testInputs.Length; i++) {
				var p = new TokenSequence(tokenizer.Parse(testInputs[i]).ToArray());
				var matches = matcher.GetMatches(new MatchBag(p)).ToArray();
				var matchBag = new MatchBag(p, matches);
				var cmpMatches = comparisonMatcher.GetMatches(matchBag).ToArray();
				var cmpMatchesStr = String.Join(";", cmpMatches.Select(m => m.ToString()).ToArray());
				Assert.Equal(expectedComparisonOutputs[i], cmpMatchesStr);

				foreach (var m in cmpMatches)
					matchBag.Add(m);
				var grpMatches = new List<Match>();
				while (true) {
					var passGrpMatches = groupMatcher.GetMatches(matchBag).ToArray();
					if (passGrpMatches.Length==0)
						break;
					foreach (var m in passGrpMatches) {
						matchBag.Add(m);
						grpMatches.Add(m);
					}
				}
				var grpMatchesStr = String.Join(";", grpMatches.Select(m => m.ToString()).ToArray());
				//Output.WriteLine(grpMatchesStr);
				Assert.Equal(expectedGrpOutputs[i], grpMatchesStr);

			}
		}

		public class ComparisonMatch : Match {
			public Match Left;
			public ComparisonMatcher.ComparisonType Cmp;
			public Match Right;
			public ComparisonMatch(Match left, ComparisonMatcher.ComparisonType cmp, Match right) {
				Left = left;
				Cmp = cmp;
				Right = right;
			}
			public override string ToString() {
				return $"{Left.ToString()}[{Cmp}]{Right.ToString()}";
			}
		}


	}
}
