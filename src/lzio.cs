﻿using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {

		public const int EOZ = -1;


        public class MBuffer {
            public byte[] buffer;
            public int n;
            public int buffsize;
        }


        public static void luaZ_initbuffer (lua_State L, MBuffer buff) { buff.buffer = null; buff.buffsize = 0; }

		public static byte[] luaZ_buffer (MBuffer buff) { return buff.buffer; }

		public static int luaZ_sizebuffer (MBuffer buff) { return buff.buffsize; }

		public static int luaZ_bufflen (MBuffer buff) { return buff.n; }

		public static void luaZ_buffremove (MBuffer buff, int i) { buff.n -= i; }

		public static void luaZ_resetbuffer (MBuffer buff) { buff.n = 0; }

		public static void luaZ_resizebuffer (lua_State L, MBuffer buff, int size) {
			buff.buffer = luaM_reallocv<byte> (L, buff.buffer, buff.buffsize, size);
			buff.buffsize = size;
		}

		public static void luaZ_freebuffer (lua_State L, MBuffer buff) { luaZ_resizebuffer (L, buff, 0); }
		

		public static byte[] luaZ_openspace (lua_State L, MBuffer buff, int n) {
			if (n > buff.buffsize) {
				if (n < LUA_MINBUFFER) n = LUA_MINBUFFER;
				luaZ_resizebuffer (L, buff, n);
			}
			return buff.buffer;
		}
    }
}
