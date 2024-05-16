/*
 *  Copyright 2016-2019 Vitaliy Fedorchenko (nrecosite.com)
 *
 *  Licensed under NLQuery Source Code License (see LICENSE file).
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS 
 *  OF ANY KIND, either express or implied.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using NReco.NLQuery.Matchers;

namespace NReco.NLQuery.Table
{
	/// <summary>
	/// Represents table match.
	/// </summary>
	public class TableMatch : Match {
		public TableSchema Table { get; set; }

		public TableMatch(TableSchema t) {
			Table = t;
		}

		public override string ToString() {
			return String.Format("Table[{0}]", Table.Name);
		}
	}

	/// <summary>
	/// Represents column match.
	/// </summary>
	public class ColumnMatch : Match {
		public ColumnSchema Column { get; set; }

		public string MatchedCaption { get; set; }

		public ColumnMatch(ColumnSchema c) {
			Column = c;
		}

		public override string ToString() {
			return String.Format("Column[{0}]", Column.Name);
		}

	}

	/// <summary>
	/// Represents column condition match.
	/// </summary>
	public class ColumnConditionMatch : Match {
		public ColumnSchema Column { get; set; }
		public ConditionType Condition { get; set; }

		Match _Value = null;
		public Match Value {
			get => _Value ?? this;
			set => _Value = value;
		}

		public Match Hint { get; set; }

		public string MatchedValue { get; set; }

		public ColumnConditionMatch() { }

		public ColumnConditionMatch(ColumnSchema col, ConditionType cnd, Match val) {
			Column = col;
			Condition = cnd;
			Value = val;
		}

		public override string ToString() {
			var val = Start.Value;
			if (this != Value) {
				val = Value.ToString();
			} else {
				if (Start != End)
					val += "..." + End.Value;
			}
			var matchedVal = MatchedValue != null && val!=MatchedValue ? " in '" + MatchedValue+"'" : String.Empty;
			return $"Column[{Column.Name} {Condition.ToString().ToLower()} '{val}'{matchedVal}]";
		}

		public enum ConditionType {
			Contains = 0,
			StartsWith = 1,
			Exact = 2,

			Equal = 32* 1,
			LessThan = 32* 2,
			GreaterThan = 32* 4,
			LessThanOrEqual = 32*1 + 32*2,
			GreaterThanOrEqual = 32*4 + 32*1,
			NotEqual = 32*8,
			Like = 32*16
		}
	}

	/// <summary>
	/// Represents and/or group of matches.
	/// </summary>
	public class GroupMatch : Match {
		public GroupMatcher.GroupType Group { get; private set; }
		public List<Match> Matches { get; private set; }

		public GroupMatch(GroupMatcher.GroupType group, params Match[] matches) {
			Group = group;
			Matches = new List<Match>();
			if (matches != null)
				Matches.AddRange(matches);
		}

		public override string ToString() {
			var mStr = String.Join(";", Matches.Select(m=>m.ToString()).ToArray() );
			return $"Group[{Group.ToString()}:{mStr}]";
		}

		public static bool IsAlreadyInGroup(MatchBag matchBag, Match left, Match right) {
			foreach (var m in matchBag.Matches) {
				if (m is GroupMatch grpM && grpM.Matches.Count==2)
					if (grpM.Matches[0]==left && grpM.Matches[1]==right)
						return true;
			}
			return false;
		}

	}


}
