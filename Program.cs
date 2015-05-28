using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cc = cclua53.cclua;

namespace cclua {
    class Program {
        static void Main (string[] args) {
            var L = cc.luaL_newstate ();
        }
    }
}
