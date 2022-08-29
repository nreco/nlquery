/*
 *  Copyright 2015-2018 Vitaliy Fedorchenko (nrecosite.com)
 *
 *  Licensed under NLQuery Source Code Licence (see LICENSE file).
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS 
 *  OF ANY KIND, either express or implied.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using System.IO;
using System.Web.Caching;


using NReco.PivotData;
using NReco.PivotData.Input;
using NReco.PivotData.Output;

namespace NReco.NLQuery.Examples.NliPivotTable.Controllers {
	
	public class PivotController : Controller {

		public ActionResult SearchDrivenReportBuilder() {
			var dataCube = LoadDataCube("cube");
			var pvtDataFactory = new PivotDataFactory();
			return View(pvtDataFactory.GetConfiguration(dataCube));
		}

		public ActionResult PivotTableHtml(string searchQuery) {
			var dataCube = LoadDataCube("cube");
			var queryParser = GetQueryParser("cube");

			PivotReport pvtReport = new PivotReport() {
				Rows = new string[0],
				Columns = new string[0],
				Measures = new[] { 0 }
			};
			if (!String.IsNullOrWhiteSpace(searchQuery)) {
				var topReports = queryParser.Parse(searchQuery, 1);
				if (topReports.Length>0) {
					pvtReport = topReports[0];
				}
			}
			// use first measure if no measures matched
			if (pvtReport.Measures.Length == 0)
				pvtReport.Measures = new[] { 0 };  // index #0

			// slice cube
			var reportOlapQuery = new SliceQuery(dataCube);
			foreach (var dim in pvtReport.Rows.Union(pvtReport.Columns).Distinct())
				reportOlapQuery.Dimension(dim);
			var reportCube = reportOlapQuery.Execute();

			if (!String.IsNullOrEmpty(pvtReport.Filter)) {
				reportCube = new CubeKeywordFilter(pvtReport.Filter).Filter(reportCube);
			}
			var pvtTblFactory = new PivotTableFactory();
			var pvtTbl = pvtTblFactory.Create(reportCube, pvtReport);

			// lets apply top limits to avoid output of huge table
			var topLimitsPvtTbl = new TopPivotTable(pvtTbl, 1000, 1000);

			return Content(RenderPivotTableHtml(topLimitsPvtTbl));
		}

		/// <summary>
		/// Suggest by prefix known keywords.
		/// </summary>
		public ActionResult SuggestKeywords(string term, int maxResults) {
			var parser = GetQueryParser("cube");
			var res = parser.SuggestKeywords(term, maxResults);
			return Json(res);
		}

		string RenderPivotTableHtml(IPivotTable pvtTbl) {
			var strWr = new StringWriter();
			var pvtHtmlWr = new PivotTableHtmlWriter(strWr);
			
			pvtHtmlWr.RenderSortIndexAttr = true; // for client side sorting
			pvtHtmlWr.FormatValue = (aggr,idx) => { return String.Format("{0:#,0.##}", aggr.Value); };
			pvtHtmlWr.FormatKey = (key, dim) => {
				var kStr = Convert.ToString(key);
				return String.IsNullOrWhiteSpace(kStr) ? "(empty)" : kStr;
			};
			pvtHtmlWr.Write(pvtTbl);
			return strWr.ToString();
		}

		// in this example serialized PivotData cube is used for the sake of simplicity
		// for large datasets data should be aggrated on the fly, only for dimensions/measures needed by the report
		IPivotData LoadDataCube(string cubeFile) {
			var cubePath = Path.Combine( HttpContext.Server.MapPath( "~/App_Data/"), cubeFile);
			var cacheKey = String.Format( "PivotData:{0}:", cubePath );
			var pvtData = HttpRuntime.Cache.Get(cacheKey) as IPivotData;
			if (pvtData == null) { 
				var cubeRdr = new CubeFileReader( cubePath );

				// if you already have PivotData Toolkit license key, set 0 as threshold value
				cubeRdr.FixedPivotDataThreshold = 1000000;

				pvtData = cubeRdr.Read();

				// in case of many PivotData values take first 100k to satisfy PivotData Trial limitations (max 100k rows)
				// this is not needed if you have PivotData Toolkit license key
				if (pvtData.Count>100000) {
					var sliceQuery = new SliceQuery(pvtData);
					var entriesCount = 0;
					sliceQuery.Where((entry) => {
						entriesCount++;
						return entriesCount < 100000;
					});
					pvtData = sliceQuery.Execute();
				}


				HttpRuntime.Cache.Add(cacheKey, pvtData, null, Cache.NoAbsoluteExpiration, new TimeSpan(0,2,0), CacheItemPriority.BelowNormal, null);
			}
			return pvtData;
		}

		QueryParser GetQueryParser(string cubeFile) {
			var cacheKey = "query_parser_"+ cubeFile;
			var parser = HttpRuntime.Cache.Get(cacheKey) as QueryParser;
			if (parser == null) {
				parser = new QueryParser(LoadDataCube(cubeFile));
				HttpRuntime.Cache[cacheKey] = parser;
			}
			return parser;
		}


	}
}
