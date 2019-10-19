﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImageFramework.Annotations;
using ImageFramework.Controller;
using ImageFramework.DirectX;
using ImageFramework.ImageLoader;
using ImageFramework.Model.Export;
using ImageFramework.Model.Filter;
using ImageFramework.Model.Shader;
using ImageFramework.Utility;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = ImageFramework.DirectX.Device;

namespace ImageFramework.Model
{
    public class Models : IDisposable, INotifyPropertyChanged
    {
        public static readonly CultureInfo Culture = new CultureInfo("en-US");

        public readonly int NumPipelines;
        public ImagesModel Images { get; }
        public FiltersModel Filter { get; }
        public IReadOnlyList<ImagePipeline> Pipelines { get; }

        public int NumEnabled => Pipelines.Count(pipe => pipe.IsEnabled);

        public ExportModel Export { get; }

        public ProgressModel Progress { get; }

        private ThumbnailModel thumbnail;

        internal TextureCache TextureCache { get; }

        private readonly List<ImagePipeline> pipelines = new List<ImagePipeline>();

        private readonly PipelineController pipelineController;

        private readonly PixelValueShader pixelValueShader;

        private readonly PreprocessModel preprocess;

        public Models(int numPipelines = 1)
        {
            NumPipelines = numPipelines;

            CheckDeviceCapabilities();
            pixelValueShader = new PixelValueShader();

            // models
            Images = new ImagesModel();
            Export = new ExportModel();
            Progress = new ProgressModel();
            Filter = new FiltersModel();
            preprocess = new PreprocessModel();
            TextureCache = new TextureCache(Images);
            thumbnail = new ThumbnailModel();

            for (int i = 0; i < numPipelines; ++i)
            {
                pipelines.Add(new ImagePipeline(i));
                pipelines.Last().PropertyChanged += PipeOnPropertyChanged;
            }
            Pipelines = pipelines;

            // pipeline controller
            pipelineController = new PipelineController(this);
        }

        private void PipeOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ImagePipeline.IsEnabled):
                    OnPropertyChanged(nameof(NumEnabled));
                    break;
            }
        }

        /// <summary>
        /// creates a new filter from a file.
        /// this filter is not added to the filter list
        /// </summary>
        /// <param name="filename">filter filename</param>
        /// <returns></returns>
        public FilterModel CreateFilter(string filename)
        {
            var loader = new FilterLoader(filename);
            
            return new FilterModel(loader, NumPipelines);
        }

        /// <inheritdoc cref="PixelValueShader.Run"/>
        public Color GetPixelValue(TextureArray2D image, int x, int y, int layer = 0, int mipmap = 0, int radius = 0)
        {
            return pixelValueShader.Run(image, x, y, layer, mipmap, radius);
        }

        /// <inheritdoc cref="ThumbnailModel.CreateThumbnail"/>
        /// Image format will be BGRA8 because this is the format expected for windows bitmaps
        public TextureArray2D CreateThumbnail(int size, TextureArray2D texture, int layer = 0)
        {
            return thumbnail.CreateThumbnail(size, texture, Format.B8G8R8A8_UNorm_SRgb, layer);
        }

        /// <summary>
        /// gets statistics about from the image
        /// </summary>
        /// <returns></returns>
        public StatisticsModel GetStatistics(TextureArray2D image, int layer = 0, int mipmap = 0)
        {
            return preprocess.GetStatistics(image, layer, mipmap, pixelValueShader, TextureCache);
        }

        /// <summary>
        /// loads image and adds it to Images 
        /// </summary>
        /// <param name="filename"></param>
        /// <exception cref="Exception"></exception>
        public void AddImageFromFile(string filename)
        {
            using (var image = IO.LoadImage(filename))
            {
                var tex = new TextureArray2D(image);

                try
                {
                    Images.AddImage(tex, filename, image.OriginalFormat);
                }
                catch (Exception)
                {
                    tex.Dispose();
                    throw;
                }
            }
        }

        /// <summary>
        /// exports a pipeline image with the given format and extension.
        /// Apply will be called by this method if required
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="extension"></param>
        /// <param name="format"></param>
        /// <param name="pipelineId"></param>
        public void ExportPipelineImage(string filename, string extension, GliFormat format, int pipelineId = 0)
        {
            var desc = new ExportDescription(filename, extension, Export) {FileFormat = format};
            if(!Pipelines[pipelineId].IsValid)
                throw new Exception($"current image formula is invalid. At least " +
                                    $"{Math.Max(Math.Max(Pipelines[pipelineId].Color.MaxImageId, Pipelines[pipelineId].Alpha.MaxImageId), 1)} " +
                                    $"images are required for it to be valid");

            // apply changes before exporting
            Apply();
            Export.Export(Pipelines[pipelineId].Image, desc);
        }

        /// <summary>
        /// returns ids of all enabled pipeline
        /// </summary>
        public List<int> GetEnabledPipelines()
        {
            var res = new List<int>();
            for(int i = 0; i < Pipelines.Count; ++i)
                if(Pipelines[i].IsEnabled)
                    res.Add(i);

            return res;
        }

        /// <summary>
        /// returns the id of the first enabled pipeline
        /// throws exception if nothing is visible
        /// </summary>
        public int GetFirstEnabledPipeline()
        {
            for(int i = 0; i < Pipelines.Count; ++i)
                if (Pipelines[i].IsEnabled)
                    return i;
            throw new Exception("no pipeline enabled");
        }

        /// <summary>
        /// Forces all pending pipeline changes to be computed
        /// </summary>
        public void Apply()
        {
            using (var cts = new CancellationTokenSource())
            {
                ApplyAsync(cts.Token).Wait();
            }
        }

        /// <summary>
        /// Forces all pending pipeline changes to be computed
        /// </summary>
        public async Task ApplyAsync(CancellationToken ct)
        {
            await pipelineController.UpdateImagesAsync(ct);
        }

        /// <summary>
        /// checks if this graphics card supports the relevant features
        /// </summary>
        private void CheckDeviceCapabilities()
        {
            var dev = Device.Get();
            // check supported format capabilities
            foreach (var f in IO.SupportedFormats)
            {
                var sup = dev.CheckFormatSupport(f);

                if ((sup & FormatSupport.Texture2D) == 0)
                    throw new Exception($"Texture2D support for {f} is required");
                // TODO this can be optional
                if ((sup & FormatSupport.MipAutogen) == 0)
                    throw new Exception($"MipAutogen support for {f} is required");

                if ((sup & FormatSupport.RenderTarget) == 0)
                    throw new Exception($"RenderTarget support for {f} is required");

                if (f == Format.R32G32B32A32_Float)
                {
                    if((sup & FormatSupport.TypedUnorderedAccessView) == 0)
                        throw new Exception($"TypesUnorderedAccess support for {f} is required");
                }
            }
        }

        public virtual void Dispose()
        {
            Export?.Dispose();
            TextureCache?.Dispose();
            Images?.Dispose();
            Filter?.Dispose();
            preprocess?.Dispose();
            foreach (var imagePipeline in pipelines)
            {
                imagePipeline.Dispose();
            }
            pipelineController?.Dispose();
            pixelValueShader?.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
