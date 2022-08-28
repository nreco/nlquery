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
using System.Globalization;

namespace NReco.NLQuery.Matchers {
	
	/// <summary>
	/// Generic date matcher.
	/// </summary>
	public class DateMatcher : IMatcher {

		public bool FirstPassOnly => true;
		public bool Recursive => false;

		public DateTimeFormatInfo DateFormat { get; set; }

		public int BoostYearWindow { get; set; }

		public DateMatcher() {
			BoostYearWindow = 100;
			DateFormat = CultureInfo.InvariantCulture.DateTimeFormat;
		}

		private DateTimeFormatInfo GetDateFormat() {
			return DateFormat ?? CultureInfo.CurrentCulture.DateTimeFormat;
		}

		public IEnumerable<Match> GetMatches(MatchBag matchBag) {
			var start = new DateMatchState(this, new DateMatch());
			return MatchHelper.RunStateMachine(matchBag.Statement.Tokens, start);
		}

		bool CheckDaysInMonth(DateMatch date) {
			var maxDays = DateFormat.Calendar.GetDaysInMonth(date.Year.Value, date.Month.Value);
			return date.Day.Value <= maxDays;
		}

		bool IsValidDate(DateMatch date) {
			if (date.Year.HasValue) {
				if (date.Month.HasValue && date.Day.HasValue) // year+month+day
					return CheckDaysInMonth(date);
				if (date.Month.HasValue) // year+month
					return true;
				if (!date.Day.HasValue) // only year
					return true;
				// do not allow year+day
				return false;
			} else
				return date.Score>Match.ScoreMaybe;
		}

		int IndexOf(string[] arr, string s) {
			for (int i=0; i<arr.Length; i++)
				if (arr[i].Equals(s, StringComparison.OrdinalIgnoreCase))
					return i;
			return -1;
		}

		int TryParseMonthName(string s) {
			var df = DateFormat;
			var monthArrs = new [] {
				df.MonthNames, df.MonthGenitiveNames, df.AbbreviatedMonthNames, df.AbbreviatedMonthGenitiveNames
			};
			for (int i=0; i<monthArrs.Length; i++) {
				var mIdx = IndexOf(monthArrs[i], s);
				if (mIdx>=0)
					return mIdx+1;
			}
			return -1;
		}

		internal class DateMatchState : IMatchState {

			DateMatch CurrentDate;
			bool PrevPunctuation;
			bool Finish;
			DateMatcher Matcher;

			internal DateMatchState(DateMatcher matcher, DateMatch date) {
				CurrentDate = date;
				Matcher = matcher;
			}

			public Match GetResult() {
				if (CurrentDate.Start==null || !Finish)
					return null;
				return CurrentDate;
			}

			void AddToken(DateMatch d, Token t, float score) {
				if (d.Start == null)
					d.Start = t;
				d.End = t;

				int partsCount = 0;
				if (d.Year.HasValue)
					partsCount++;
				if (d.Month.HasValue)
					partsCount++;
				if (d.Day.HasValue)
					partsCount++;

				if (partsCount==0) {
					d.Score = score;
				} else {
					d.Score = ((d.Score*partsCount) + score) / (partsCount+1);
				}
			}

			public IEnumerable<IMatchState> Next(Token t) {
				// propagate start state
				if (CurrentDate.Start == null)
					yield return this;

				int nextStates = 0;
				switch (t.Type) {
					case TokenType.Separator:
						if (CurrentDate.Start != null) {
							nextStates++;
							yield return this;
						}
						break;
					case TokenType.Math:  // for "/"
					case TokenType.Punctuation:
						// skip spaces or punctuation and continue
						var isTypicalForDate = t.Value == "/" || t.Value == "." || t.Value=="-";
						if (CurrentDate.Start != null && !PrevPunctuation && (isTypicalForDate || t.Value==",")) {
							PrevPunctuation = true;
							if (isTypicalForDate && CurrentDate.Score <= Match.ScoreMaybe)
								CurrentDate.Score += 0.1f; // boost
							nextStates++;
							yield return this;
						}
						break;
					case TokenType.Word:
						if (!CurrentDate.Month.HasValue) {
							// this can be only month name
							var month = Matcher.TryParseMonthName(t.Value);
							if (month >= 0) {
								var d = new DateMatch(CurrentDate);
								AddToken(d, t, Match.ScoreCertain);
								d.Month = month;
								nextStates++;
								yield return new DateMatchState(Matcher, d);
							}
						}
						break;
					case TokenType.Number:
						int num;
						if (Int32.TryParse(t.Value, out num)) {
							// year
							if (!CurrentDate.Year.HasValue && t.Value.Length == 4) {
								var score = Match.ScoreCertain;
								if (Matcher.BoostYearWindow > 0) {
									var boost = ((float)Math.Min(Matcher.BoostYearWindow, Math.Abs(num - DateTime.Now.Year))) / Matcher.BoostYearWindow;
									score -= boost / 4;
								}
								var d = new DateMatch(CurrentDate);
								AddToken(d, t, score);
								d.Year = num;
								nextStates++;
								yield return new DateMatchState(Matcher, d);
							}
							// month number
							if (!CurrentDate.Month.HasValue && num >= 1 && num <= 12) {
								var d = new DateMatch(CurrentDate);
								AddToken(d, t, Match.ScoreMaybe);
								d.Month = num;
								nextStates++;
								yield return new DateMatchState(Matcher, d);
							}
							// day number
							if (!CurrentDate.Day.HasValue && num >= 1 && num <= 31) {
								var d = new DateMatch(CurrentDate);
								AddToken(d, t, Match.ScoreMaybe);
								d.Day = num;
								nextStates++;
								yield return new DateMatchState(Matcher, d);
							}

						}
						break;
				}

				if (CurrentDate.Start != null && nextStates == 0 && Matcher.IsValidDate(CurrentDate))
					Finish = true;

			}

		}

	}
}
