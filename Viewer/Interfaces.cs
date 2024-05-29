using Silk.NET.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viewer
{
    internal interface IDraw
    {
        // TODO: Params
        void Draw();
    }

    internal struct KeyHelp(string keys, string desc, string state)
    {
        public string Keys { get; set; } = keys;
        public string Description { get; set; } = desc;
        public string State { get; set; } = state;
    }

    internal interface IKeyReceiver
    {
        void Key(Key key, bool isDown);
        List<KeyHelp> GetHelp();
    }
}
