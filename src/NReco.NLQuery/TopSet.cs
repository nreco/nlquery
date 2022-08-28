/*
 *  Copyright 2016 Vitaliy Fedorchenko (nrecosite.com)
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

namespace NReco.NLQuery {
	
	/// <summary>
	/// Represents a collection that holds only top N elements.
	/// </summary>
	public class TopSet<T> {

		Comparison<T> Compare;
		int MaxSize;
		T[] Elements;

		int Length = 0;
		int MinIndex = -1;
		int MaxIndex = -1;

		/// <summary>
		/// Gets the maximum value in the collection.
		/// </summary>
		public T Max {
			get {
				return MaxIndex>=0 ? Elements[MaxIndex] : default(T);
			}
		}

		/// <summary>
		/// Gets the minimum value in the collection.
		/// </summary>
		public T Min {
			get {
				return MinIndex>=0 ? Elements[MinIndex] : default(T);
			}
		}

		/// <summary>
		/// Returns number of elements in the collection.
		/// </summary>
		public int Count {
			get {
				return Length;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TopSet"/> class.
		/// </summary>
		/// <param name="maxSize">max number of elements</param>
		/// <param name="comparer">comparer that used for determining top elements</param>
		public TopSet(int maxSize, IComparer<T> comparer) : this(maxSize, comparer.Compare) {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TopSet"/> class.
		/// </summary>
		/// <param name="maxSize">max number of elements</param>
		/// <param name="compare">comparer that used for determining top elements</param>
		public TopSet(int maxSize, Comparison<T> compare) {
			Compare = compare;
			MaxSize = maxSize;
			Elements = new T[MaxSize];
		}

		/// <summary>
		/// Adds an element into the <see cref="TopSet"/>.
		/// </summary>
		/// <param name="elem">element to add</param>
		/// <returns>true if item is added to the collection; otherwise, false.</returns>
		public bool Add(T elem) {
			if (Length < MaxSize) {
				int i = Length++;
				Elements[i] = elem;
				RefreshMin(elem, i);
				RefreshMax(elem, i);
				return true;
			} else {
				if (Compare(elem, Elements[MinIndex]) > 0) {
					// replace min
					Elements[MinIndex] = elem;
					RefreshMax(elem, MinIndex);
					var newMinIdx = 0;
					for (int i = 1; i < Elements.Length; i++) {
						if (Compare(Elements[i], Elements[newMinIdx]) < 0) {
							newMinIdx = i;
						}
					}
					MinIndex = newMinIdx;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Copies top N elements to a new array. Elements are sorted in descending order. 
		/// </summary>
		/// <returns>array with top elements.</returns>
		public T[] ToArray() {
			var copy = new T[Length];
			Array.Copy(Elements, copy, Length);
			Array.Sort(copy, Comparer<T>.Create( (a,b) => Compare(b,a) ) );
			return copy;
		}

		private void RefreshMin(T elem, int i) {
			if (MinIndex >= 0) {
				if (Compare(elem, Elements[MinIndex])<0)
					MinIndex = i;
			} else {
				MinIndex = i;
			}
		}
		private void RefreshMax(T elem, int i) {
			if (MaxIndex >= 0) {
				if (Compare(elem, Elements[MaxIndex])>0)
					MaxIndex = i;
			} else {
				MaxIndex = i;
			}
		}



	}
}
