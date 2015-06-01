using System;

namespace cclua {

    public static partial class lua530 {

		public static long LUA_VERSION_NUM = 503;


        /*
        ** pseudo-indices
        */
        public const int LUA_REGISTRYINDEX = imp.LUAI_FIRSTPSEUDOIDX;
        public static int lua_upvalueindex (int i) { return (LUA_REGISTRYINDEX - i); }


        /* thread status */
        public const int LUA_OK = 0;
        public const int LUA_YIELD = 1;
        public const int LUA_ERRRUN = 2;
        public const int LUA_ERRSYNTAX = 3;
        public const int LUA_ERRMEM = 4;
        public const int LUA_ERRGCMM = 5;
        public const int LUA_ERRERR = 6;

        /*
        ** basic types
        */
        public const int LUA_TNONE = -1;
        public const int LUA_TNIL = 0;
        public const int LUA_TBOOLEAN = 1;
        public const int LUA_TLIGHTUSERDATA = 2;
        public const int LUA_TNUMBER = 3;
        public const int LUA_TSTRING = 4;
        public const int LUA_TTABLE = 5;
        public const int LUA_TFUNCTION = 6;
        public const int LUA_TUSERDATA = 7;
        public const int LUA_TTHREAD = 8;
        public const int LUA_NUMTAGS = 9;

        /* minimum Lua stack available to a C function */
        public const int LUA_MINSTACK = 20;

        /* predefined values in the registry */
        public const int LUA_RIDX_MAINTHREAD = 1;
        public const int LUA_RIDX_GLOBALS = 2;
        public const int LUA_RIDX_LAST = LUA_RIDX_GLOBALS;

        /*
        ** Type for C functions registered with Lua
        */
        public delegate int lua_CFunction (lua_State L);

        /*
        ** Type for continuation functions
        */
        public delegate int lua_KFunction (lua_State L, int status, long ctx);

        /* Functions to be called by the debugger in specific events */
        public delegate void lua_Hook (lua_State L, lua_Debug ar);

        public class lua_Debug {
            public int devent;
            public string name;  /* (n) */
            public string namewhat;  /* (n) 'global', 'local', 'field', 'method' */
            public string what;  /* (S) 'Lua', 'C', 'main', 'tail' */
            public string source;  /* (S) */
            public int currentline;  /* (l) */
            public int linedefined;  /* (S) */
            public int lastlinedefined;  /* (S) */
            public byte nups;  /* (u) number of upvalues */
            public byte nparams;  /* (u) number of parameters */
            public sbyte isvararg;  /* (u) */
            public sbyte istailcall;  /* (t) */
            public sbyte[] short_src;  /* (S) */
            /* private part */
            public imp.CallInfo i_ci;  /* active function */
        };

        public static string lua_tostring (lua_State L, int idx) {
            ulong m = 0;
            return lua_tolstring (L, idx, ref m);
        }
    }
}
