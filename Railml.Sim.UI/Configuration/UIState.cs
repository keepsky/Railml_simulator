using System;
using System.IO;
using System.Text.Json;

namespace Railml.Sim.UI.Configuration
{
    public class UIState
    {
        public double WindowTop { get; set; } = 100;
        public double WindowLeft { get; set; } = 100;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public bool IsLogVisible { get; set; } = true;
        public double LogHeight { get; set; } = 150.0;
        public double TimeScale { get; set; } = 1.0;

        public static UIState Load(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<UIState>(json) ?? new UIState();
                }
            }
            catch { }
            return new UIState();
        }

        public void Save(string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(filePath, json);
            }
            catch { }
        }
    }
}
