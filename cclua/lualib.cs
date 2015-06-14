using System;

namespace cclua {

    public static partial class imp {

		public static string LUA_COLIBNAME = "coroutine";
		public static string LUA_TABLIBNAME = "table";
		public static string LUA_IOLIBNAME = "io";
		public static string LUA_OSLIBNAME = "os";
		public static string LUA_STRLIBNAME = "string";
		public static string LUA_UTF8LIBNAME = "utf8";
		public static string LUA_MATHLIBNAME = "math";
		public static string LUA_DBLIBNAME = "debug";
		public static string LUA_LOADLIBNAME = "package";


        public static void lua_assert (bool x) {
			throw (new Exception ());
        }
    }
}
