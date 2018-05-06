﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextureViewer.Controller.TextureViews
{
    interface ITextureView
    {
        void Draw();
        void Dispose();
    }
}