using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer
{
    internal class GLStats : IDisposable
    {
        private readonly GL _gl;
        private uint _handle;

        private long _lastValue;
        private bool _dirty;
        public long LastValue {
            get {
                if (!_dirty)
                    return _lastValue;

                _lastValue = _gl.GetQueryObject(_handle, QueryObjectParameterName.Result);
                _dirty = false;
                return _lastValue;
            }
        }


        public GLStats(GL gl)
        {
            _gl = gl;
            _handle = _gl.GenQuery();
        }

        public void BeginFrame()
        {
            _gl.BeginQuery(QueryTarget.PrimitivesGenerated, _handle);
        }

        public void EndFrame()
        {
            _gl.EndQuery(QueryTarget.PrimitivesGenerated);
        }

        public void Dispose()
        {
            _gl.DeleteQuery(_handle);
        }
    }
}
