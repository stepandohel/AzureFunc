using Newtonsoft.Json.Linq;
using PBIX_to_Flat.OutputModels;

namespace PBIX_to_Flat
{
    public class OutputObject
    {
        public OutputObject(JObject input)
        {
            Filters = new List<Filter>();
            Visuals = new List<Visual>();
            Measures = new List<LocalMeasure>();

            string? rptConfig = input["config"]?.ToString();

            if (rptConfig != null)
            {
                string rptMeasureFormattedJson = JToken.Parse(rptConfig).ToString();
                JObject rptMeasureJson = JObject.Parse(rptMeasureFormattedJson);

                foreach (JToken modelExtension in rptMeasureJson["modelExtensions"].Children())
                {
                    foreach (var entity in modelExtension["entities"].Children())
                    {
                        foreach (var measure in entity["measures"].Children())
                        {
                            Measures.Add(new LocalMeasure
                            {
                                ReportIdentifier = _reposrtIdentifier,
                                TableName = entity["name"].ToString(),
                                MeasureName = measure["name"].ToString(),
                                DAXDefinition = measure["expression"].ToString()
                            });
                        }
                    }
                }
            }

            string rptFilters = input["filters"].ToString();

            try
            {
                string formattedrptfiltersJson = JToken.Parse(rptFilters).ToString();
                JArray rptFiltersJson = JArray.Parse(formattedrptfiltersJson);

                AddFilters(rptFiltersJson, "ReportFilter", null, null, null, null);
            }
            catch
            {
            }

            // Pages
            if (input["sections"] != null)
            {
                foreach (var o in input["sections"].Children())
                {
                    string? pageId = o["name"]?.ToString();
                    string? pageName = o["displayName"]?.ToString();

                    if (o["filters"] != null)
                    {
                        string? pageFlt = o["filters"].ToString();

                        string formattedpagfltJson = JToken.Parse(pageFlt).ToString();
                        var pageFltJson = JArray.Parse(formattedpagfltJson);

                        // Page-Level Filters
                        AddFilters(pageFltJson, "PageFilter", pageId, pageName, null, null);
                    }

                    // Visuals
                    foreach (var vc in o["visualContainers"].Children())
                    {
                        string config = (string)vc["config"];
                        string formattedconfigJson = JToken.Parse(config).ToString();
                        var configJson = JObject.Parse(formattedconfigJson);
                        string visualId = (string)configJson["name"];
                        string visualType = string.Empty;
                        string visualName = string.Empty;
                        string parentGroup = string.Empty;

                        if (configJson["singleVisual"]?["visualType"] != null)
                        {
                            visualType = (string)configJson["singleVisual"]["visualType"];
                        }
                        else
                        {
                            visualType = "visualGroup";
                        }

                        // Visual Name
                        if (configJson["singleVisualGroup"]?["displayName"] != null)
                        {
                            visualName = (string)configJson["singleVisualGroup"]["displayName"];
                        }
                        else if (configJson["singleVisual"]?["vcObjects"]?["title"]?[0]?["properties"]?["text"]?["expr"]?["Literal"]?["Value"] != null)
                        {
                            visualName = (string)configJson["singleVisual"]["vcObjects"]["title"][0]["properties"]["text"]["expr"]["Literal"]["Value"];
                            visualName = visualName.Substring(1, visualName.Length - 2);
                        }
                        else
                        {
                            visualName = visualType;
                        }

                        try
                        {
                            if (configJson["singleVisual"]?["prototypeQuery"]?["Select"] != null)
                            {
                                foreach (var o2 in configJson["singleVisual"]["prototypeQuery"]["Select"].Children())
                                {
                                    string tableName = string.Empty;
                                    string objectName = string.Empty;
                                    string src = string.Empty;

                                    try
                                    {
                                        if (o2["Column"] != null)
                                        {
                                            objectName = (string)o2["Column"]["Property"];
                                            src = (string)o2["Column"]["Expression"]["SourceRef"]["Source"];
                                        }
                                        else if (o2["Measure"] != null)
                                        {
                                            objectName = (string)o2["Measure"]["Property"];
                                            src = (string)o2["Measure"]["Expression"]["SourceRef"]["Source"];
                                        }
                                        else if (o2["HierarchyLevel"] != null)
                                        {
                                            string levelName = (string)o2["HierarchyLevel"]["Level"];
                                            string hierName = (string)o2["HierarchyLevel"]["Expression"]["Hierarchy"]["Hierarchy"];
                                            objectName = hierName + "." + levelName;
                                            src = (string)o2["HierarchyLevel"]["Expression"]["Hierarchy"]["Expression"]["SourceRef"]["Source"];
                                        }
                                        else if (o2["Aggregation"] != null)
                                        {
                                            objectName = (string)o2["Aggregation"]["Expression"]["Column"]["Property"];
                                            src = (string)o2["Aggregation"]["Expression"]["Column"]["Expression"]["SourceRef"]["Source"];
                                        }
                                    }
                                    catch { }

                                    if (configJson["singleVisual"]?["prototypeQuery"]?["From"] != null)
                                    {
                                        foreach (var t in configJson["singleVisual"]["prototypeQuery"]["From"].Children())
                                        {
                                            string? n = t["Name"]?.ToString();
                                            string? tbl = t["Entity"]?.ToString();

                                            if (src == n)
                                            {
                                                tableName = tbl;
                                            }
                                        }
                                    }

                                    Visuals.Add(new Visual
                                    {
                                        ReportIdentifier = _reposrtIdentifier,
                                        PageId = pageId,
                                        PageName = pageName,
                                        VisualId = visualId,
                                        VisualType = visualType,
                                        TableName = tableName,
                                        ObjectName = objectName
                                    });
                                }
                            }
                        }
                        catch
                        {
                        }

                        // Visual Filters
                        string? visfilter = vc["filters"]?.ToString();

                        if (visfilter != null)
                        {
                            string formattedvisfilterJson = JToken.Parse(visfilter).ToString();
                            var visfilterJson = JArray.Parse(formattedvisfilterJson);

                            AddFilters(visfilterJson, "VisualFilter", pageId, pageName, visualId, visualType);
                        }
                    }
                }
            }
        }

        private string _reposrtIdentifier = "Sales report";

        private void AddFilters(JArray filters, string filterLevel, string? pageId, string? pageName, string? visualId, string? visualType)
        {
            foreach (var o in filters.Children())
            {
                string objName = string.Empty;
                string tblName = string.Empty;

                // Note: Add filter conditions
                try
                {
                    if (o["expression"]?["Column"] != null)
                    {
                        objName = (string)o["expression"]["Column"]["Property"];
                        tblName = (string)o["expression"]["Column"]["Expression"]["SourceRef"]["Entity"];
                    }
                    else if (o["expression"]?["Measure"] != null)
                    {
                        objName = (string)o["expression"]["Measure"]["Property"];
                        tblName = (string)o["expression"]["Measure"]["Expression"]["SourceRef"]["Entity"];
                    }
                    else if (o["expression"]?["HierarchyLevel"] != null)
                    {
                        string levelName = (string)o["expression"]["HierarchyLevel"]["Level"];
                        string hierName = (string)o["expression"]["HierarchyLevel"]["Expression"]["Hierarchy"]["Hierarchy"];
                        objName = hierName + "." + levelName;
                        tblName = (string)o["expression"]["HierarchyLevel"]["Expression"]["Hierarchy"]["Expression"]["SourceRef"]["Entity"];
                    }
                }
                catch { }

                Filters.Add(new Filter
                {
                    ReportIdentifier = _reposrtIdentifier,
                    FilterLevel = filterLevel,
                    PageId = pageId,
                    PageName = pageName,
                    VisualId = visualId,
                    VisualType = visualType,
                    TableName = tblName,
                    Column = objName,
                });
            }
        }

        public List<Filter> Filters { get; set; }
        public List<Visual> Visuals { get; set; }
        public List<LocalMeasure> Measures { get; set; }
    }
}
