using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {

        public class MBuffer {
            public byte[] buffer;
            public ulong n;
            public ulong buffsize;
        }

        public static void luaZ_initbuffer (lua_State L, MBuffer buff) {
            buff.buffer = null;
            buff.buffsize = 0;
        }


		public static void luaZ_freebuffer (lua_State L, MBuffer buff) {
		}
    }
}
