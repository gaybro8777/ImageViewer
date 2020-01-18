﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageFramework.DirectX;
using ImageFramework.Utility;
using ImageViewer.Controller.TextureViews.Shader;
using ImageViewer.Controller.TextureViews.Shared;
using ImageViewer.Models;
using ImageViewer.Models.Display;
using SharpDX;

namespace ImageViewer.Controller.TextureViews.Texture3D
{
    public class RayCastingView : Texture3DBaseView
    {
        private readonly RayCastingShader shader;
        private readonly RayMarchingShader marchingShader;
        private RayCastingDisplayModel displayEx;

        public RayCastingView(ModelsEx models) : base(models)
        {
            shader = new RayCastingShader(models);
            marchingShader = new RayMarchingShader(models);
            displayEx = (RayCastingDisplayModel)models.Display.ExtendedViewData;
        } 

        public override void Draw(ITexture texture)
        {
            if (texture == null) return;

            base.Draw(texture);

            var dev = Device.Get();
            dev.OutputMerger.BlendState = models.ViewData.AlphaDarkenState;

            if (models.Display.LinearInterpolation)
            {
                shader.Run(models.Display.ClientAspectRatio, GetWorldToImage(), CalcFarplane(), 
                texture.GetSrView(models.Display.ActiveLayer, models.Display.ActiveMipmap));
            }
            else
            {
                marchingShader.Run(models.Display.ClientAspectRatio, GetWorldToImage(), CalcFarplane(),
                displayEx.FlatShading,texture.GetSrView(models.Display.ActiveLayer, models.Display.ActiveMipmap));
            }

            dev.OutputMerger.BlendState = models.ViewData.DefaultBlendState;
        }

        private Matrix GetWorldToImage()
        {
            float aspectX = models.Images.Size.X / (float)models.Images.Size.Y;
            float aspectZ = models.Images.Size.Z / (float)models.Images.Size.Y;

            return
                Matrix.Translation(-cubeOffsetX, -cubeOffsetY, -GetCubeCenter()) * // translate cube center to origin
                GetRotation() * // undo rotation
                Matrix.Scaling(0.5f * aspectX, 0.5f, 0.5f * aspectZ) * // scale to [-0.5, 0.5]
                Matrix.Translation(0.5f, 0.5f, 0.5f); // move to [0, 1]
        }

        public override Size3 GetTexelPosition(Vector2 mouse)
        {
            return new Size3(0, 0, 0);
        }

        public override void Dispose()
        {
            shader?.Dispose();
            marchingShader?.Dispose();
        }
    }
}
