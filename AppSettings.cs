using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ProductionApiTester
{
    public class AppSettings
    {
        public static readonly (string Name, string Header)[] AllColumns =
        {
            ("Barcode",      "Barcode"),
            ("LRef",      "LRef"),
            ("ShortInfo",    "Description"),
            ("ProductId",    "Product ID"),
            ("ModelId",      "Model"),
            ("Quantity",     "Qty"),
            ("Status",       "Status"),
            ("OrderNo",      "Order No"),
            ("Customer",     "Customer"),
            ("PlannedStart", "Planned Start"),
            ("PlannedEnd",   "Planned End"),
            ("RackId",       "Rack"),
            ("BatchId",      "Batch"),
            ("ModelCode",    "Model Code"),
            ("FabricCode",   "Fabric Code"),
            ("SteeringCode", "Steering Code"),
            ("ProfileColor", "Profile Color"),
            ("Dimensions",   "Dimensions"),
        };

        public string Url { get; set; } = "http://localhost:8486/";
        public string User { get; set; } = "";
        public string WorkCenter { get; set; } = "";

        // Ordered list of visible column names. Null = all columns in default order.
        public List<string> VisibleColumns { get; set; }

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProductionApiTester", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(FilePath));
                    if (s != null) return s;
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { }
        }
    }
}
