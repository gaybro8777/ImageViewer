﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageFramework.ImageLoader;
using Newtonsoft.Json;

namespace ImageViewer.Models.Settings
{
    public class ViewerConfig
    {
        [Flags]
        public enum Components
        {
            None,
            Images = 1,
            Equations = 2,
            All = 0xFFFFFFF
        }

        public static ViewerConfig LoadFromFile(string filename)
        {
            var txt = File.ReadAllText(filename);
            return JsonConvert.DeserializeObject<ViewerConfig>(txt);
        }

        public void WriteToFile(string filename)
        {
            var txt = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(filename, txt);
        }

        public async Task ApplyToModels(ModelsEx models)
        {
            if (Images != null)
            {
                await Images.ApplyToModels(models);
            }

            Equation?.ApplyToModels(models);
        }

        public static ViewerConfig LoadFromModels(ModelsEx models, Components c)
        {
            var res = new ViewerConfig();
            if (c.HasFlag(Components.Images))
            {
                res.Images = ImagesConfig.LoadFromModels(models);
            }

            if (c.HasFlag(Components.Equations))
            {
                res.Equation = EquationConfig.LoadFromModels(models);
            }

            return res;
        }

        public ImagesConfig Images { get; set; }
        public EquationConfig Equation { get; set; }
    }
}