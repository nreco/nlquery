using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Microsoft.AspNetCore.Hosting;

using NReco.NLQuery.Examples.NliDataFilter.Data;
using NReco.NLQuery.Examples.NliDataFilter.Models;

using NReco.Data;
using NReco.NLQuery.Table;
using Microsoft.Extensions.Caching.Memory;

namespace NReco.NLQuery.Examples.NliDataFilter {
	
	public class ListController : Controller {

		ListDataRepository DataRepository { get; set; }
		IWebHostEnvironment HostEnv;
		IMemoryCache MemCache;

		public ListController(IWebHostEnvironment hostEnv, IMemoryCache memoryCache) {
			HostEnv = hostEnv;
			MemCache = memoryCache;
			DataRepository = new ListDataRepository(ConfigureDataSource());
		}

		ListContext createListContext(string searchQuery) {
			QNode filter = null;
			if (!String.IsNullOrEmpty(searchQuery)) {
				var parser = GetListQueryParser();
				var suggestedQueries = parser.Parse(searchQuery, 5);
				if (suggestedQueries.Length > 0) {
					filter = suggestedQueries.First().Condition;
				}
			}
			var listData = DataRepository.Load(new Query("OrderDetailsView", filter) { RecordCount = 20 });
			return new ListContext() {
				Data = listData,
				FilterCondition = new NReco.Data.Relex.RelexBuilder().BuildRelex(filter)
			};
		}

		public ActionResult ListPage() {
			return View(createListContext(null));
		}

		public ActionResult ListView(string searchQuery) {
			return PartialView(createListContext(searchQuery));
		}

		public ActionResult SuggestKeys(string term, int maxResults) {
			var parser = GetListQueryParser();
			var res = parser.SuggestKeywords(term, maxResults);
			return Json(res);
		}

		ListQueryParser GetListQueryParser() {
			var cacheKey = "customer_projects_query_parser";
			var parser = MemCache.Get(cacheKey) as ListQueryParser;
			if (parser == null) {
				parser = new ListQueryParser(
					// describe data table schema for recognition
					new TableSchema() {
						Name = "OrderDetailsView",
						Columns = new ColumnSchema[] {
							new ColumnSchema() {
								Caption = "Company Name",
								Name = "CompanyName",
								DataType = TableColumnDataType.String,
								Values = DataRepository.LoadDistinctValues("Customers","CompanyName").ToArray()
							},
							new ColumnSchema() {
								Caption = "Contact Name",
								Name = "ContactName",
								DataType = TableColumnDataType.String,
								Values = DataRepository.LoadDistinctValues("Customers","ContactName").ToArray()
							},
							new ColumnSchema() {
								Caption = "Product Name",
								Name = "ProductName",
								DataType = TableColumnDataType.String,
								Values = DataRepository.LoadDistinctValues("Products","ProductName").ToArray()
							},
							new ColumnSchema() {
								Caption = "Country",
								Name = "Country",
								DataType = TableColumnDataType.String,
								Values = DataRepository.LoadDistinctValues("Customers","Country").ToArray()
							},
							new ColumnSchema() {
								Caption = "Order Date",
								Name = "OrderDate",
								DataType = TableColumnDataType.Date
							},
							new ColumnSchema() {
								Caption = "Unit Price",
								Name = "UnitPrice",
								DataType = TableColumnDataType.Number
							},
							new ColumnSchema() {
								Caption = "Quantity",
								Name = "Quantity",
								DataType = TableColumnDataType.Number
							}
						}
					});
				MemCache.Set(cacheKey, parser);
			}
			return parser;
		}

		DbDataAdapter ConfigureDataSource() {
			var dbFactory = new DbFactory(Microsoft.Data.Sqlite.SqliteFactory.Instance) {
				LastInsertIdSelectText = "SELECT last_insert_rowid()"
			};
			var dbConnection = dbFactory.CreateConnection();
			dbConnection.ConnectionString = "Data Source=" + Path.Combine(HostEnv.ContentRootPath, "northwind.db");
			var dbCmdBuilder = new DbCommandBuilder(dbFactory);
			dbCmdBuilder.Views["OrderDetailsView"] = new DbDataView(@"
				select @columns from (
					select o.OrderID, c.CompanyName, c.ContactName, o.OrderDate, p.ProductName, od.UnitPrice, od.Quantity, c.Country
					from [Order Details] od
					LEFT JOIN [Orders] o ON (od.OrderId=o.OrderId)
					LEFT JOIN [Products] p ON (od.ProductId=p.ProductId)
					LEFT JOIN [Customers] c ON (c.CustomerID=o.CustomerID)
				) od @where[ WHERE {0}] @orderby[ ORDER BY {0}]"
			);
			var dbAdapter = new DbDataAdapter(dbConnection, dbCmdBuilder);
			return dbAdapter;
		}




	}
}
