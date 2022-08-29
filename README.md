# NReco.NLQuery

NReco.NLQuery provides a simple way to add search-based interface into your .NET app. It was specially designed for matching business entities in context of structured data (tabular data or database, OLAP cube, text indexes etc). This library is useful for: keyword-based filters for lists/grids, custom semantic search by database(s), search-driven analytics (reports by search), parse incoming messages by bots.

[![NuGet Release](https://img.shields.io/nuget/v/NReco.NLQuery.svg)](https://www.nuget.org/packages/NReco.NLQuery/)

* implements basic set of matchers for handling typical search queries
* handles relative date phrases like "yesterday", "last week", "last month", "last year", conditions "between", "more than", "less than", "age>18", ranges like "before", "after". Custom date-recognizer phrases may be easily added.
* supports matching in external indexes (say, Lucene or ElasticSearch).
* synonyms for better recognition results and ontology matching
* can helps users to form a query by providing autocomplete suggestions
* includes a component that automatically configures recognizer by a data table description.

## Online demos

* [Search-like list filter](http://nlquery.nrecosite.com/) (NliDataFilter example)
* [Search-driven analytics (Q&A)](http://nlquery.nrecosite.com/Pivot/SearchDrivenReportBuilder) (NliPivotTable example)
* [PivotData Microservice search queries](http://pivotdataservice.nrecosite.com/pivotdataservice/search-driven-analytics-nlq.html)

## Examples

* [NerByDataSet](https://github.com/nreco/nlquery/tree/master/examples/NReco.NLQuery.Examples.NerByDataset): Recognizes search query in context of MovieLens dataset (films).
* [NliDataFilter](https://github.com/nreco/nlquery/tree/master/examples/NReco.NLQuery.Examples.NliDataFilter): ASP.NET example for natural language query to SQL translation (data list search-like filter).
* [NliPivotTable](https://github.com/nreco/nlquery/tree/master/examples/NReco.NLQuery.Examples.NliPivotTable): ASP.NET example for search-driven analytics (Q&A report builder). This example uses [PivotData Toolkit](https://www.nrecosite.com/pivot_data_library_net.aspx) for pivot tables generation.
* [NlqForOlap](https://github.com/nreco/nlquery/tree/master/examples/NReco.NLQuery.Examples.NlqForOlap): how to parse natural language queries to build OLAP-kind of queries (no dependency on the concrete OLAP technology).


## Who is using this?
NReco.NLQuery is in production use at [SeekTable.com](https://www.seektable.com/) and [PivotData microservice](https://www.nrecosite.com/pivotdata_service.aspx).


## License
Copyright 2016-2022 Vitaliy Fedorchenko

Distributed under the NReco NLQuery FREE license (see src/LICENSE): this library can be used for free only in non-SaaS apps that have only one single-server production deployment.
In all other cases commercial license is required (can be purchased [here](https://www.nrecosite.com/nlp_ner_net.aspx)).

