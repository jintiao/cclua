using System;

namespace cclua53 {
    public static partial class imp {

        public class MBuffer {
            public byte[] buffer;
            public ulong n;
            public ulong buffsize;
        }

        public static void luaZ_initbuffer (cclua.lua_State L, MBuffer buff) {
            buff.buffer = null;
            buff.buffsize = 0;
        }
    }
}
