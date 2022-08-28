using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

using NReco.NLQuery;

namespace NReco.NLQuery.Tests
{
    public class TopSetTests
    {

		[Fact]
		public void Top10() {

			var topSet = new TopSet<int>(10, Comparer<int>.Create( (a,b)=> a.CompareTo(b) ) );
			for (int i=1; i<11; i++)
				topSet.Add(i);
			Assert.Equal(1, topSet.Min);
			Assert.Equal(10, topSet.Max);

			Assert.False( topSet.Add(0) );
			Assert.Equal(10, topSet.Count);

			var arr = new int[]{5,200,7,80,50};
			foreach (var i in arr)
				topSet.Add(i);
			Assert.Equal(10, topSet.Count);

			Assert.Equal(200, topSet.Max);

			var expectedArr = new int[] {200,80,50,10,9,8,7,7,6,5};
			var topElems = topSet.ToArray();
			for (int i=0; i<topElems.Length; i++)
				Assert.Equal(expectedArr[i],topElems[i]);
		}

    }
}
